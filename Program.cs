using System.ComponentModel;
using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

const string SkillName = "local-advising-proof";
const string LocalToolName = "GetStudentSnapshot";
const string SkillProof = "advising-demo-v1-8c37";
const string Query = "Prepare a graduation advising brief for synthetic student demo-001.";

string endpoint = Required("FOUNDRY_PROJECT_ENDPOINT").TrimEnd('/');
string model = Required("FOUNDRY_MODEL");
string toolboxName = Required("FOUNDRY_TOOLBOX_NAME");
string toolboxVersion = Required("FOUNDRY_TOOLBOX_VERSION");
_ = Required("AZURE_CONFIG_DIR");
string toolboxUrl = $"{endpoint}/toolboxes/{Uri.EscapeDataString(toolboxName)}" +
    $"/versions/{Uri.EscapeDataString(toolboxVersion)}/mcp?api-version=v1";

using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
CancellationToken cancellationToken = timeout.Token;
var credential = new AzureCliCredential();
var proof = new ExecutionProof();
string receipt = $"local-{Guid.NewGuid():N}";
var localCalls = new List<StudentCall>();
string? answer = null;
bool passed = false;

// Only this MCP transport and the Foundry model client can supply remote context.
// The runtime never reads the local SKILL.md authoring file.
using var httpClient = new HttpClient(new ToolboxHttpHandler(credential, proof));
await using McpClient mcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri(toolboxUrl),
        Name = toolboxName,
        TransportMode = HttpTransportMode.StreamableHttp,
        AdditionalHeaders = new Dictionary<string, string>
        {
            ["Foundry-Features"] = "Toolboxes=V1Preview",
        },
    }, httpClient),
    cancellationToken: cancellationToken);

using var skillsProvider = new AgentSkillsProviderBuilder()
    .UseMcpSkills(mcpClient)
    .UseFilter((skill, _) => skill.Frontmatter.Name == SkillName)
    .Build();

AIAgent agent = new AIProjectClient(new Uri(endpoint), credential)
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = "local-toolbox-skills-proof",
        ChatOptions = new ChatOptions
        {
            ModelId = model,
            Instructions = """
                You are an advising assistant working exclusively with synthetic demo data.
                Use the relevant available skill before using the local student tool.
                Follow that skill's instructions and do not invent student facts.
                """,
            Tools = [AIFunctionFactory.Create(GetStudentSnapshot, name: LocalToolName)],
        },
        AIContextProviders = [skillsProvider],
    })
    .AsBuilder()
    .UseToolApproval(new ToolApprovalAgentOptions
    {
        AutoApprovalRules = [AgentSkillsProvider.ReadOnlyToolsAutoApprovalRule],
    })
    .Use(TraceFunctionAsync)
    .Build();

Console.WriteLine($"Model: {model}");
Console.WriteLine($"Toolbox: {toolboxUrl}");
Console.WriteLine($"User: {Query}");

try
{
    AgentResponse response = await agent.RunAsync(Query, cancellationToken: cancellationToken);
    answer = response.Text;
    Console.WriteLine($"\nAssistant:\n{answer}");
    proof.Verify(SkillName, LocalToolName, SkillProof, receipt, answer, localCalls);
    passed = true;
    Console.WriteLine("\nPASS: remote skill loaded, local function invoked, skill instructions followed.");
}
finally
{
    string resultsPath = Path.GetFullPath("results");
    Directory.CreateDirectory(resultsPath);
    await File.WriteAllTextAsync(Path.Combine(resultsPath, "proof.json"),
        JsonSerializer.Serialize(new
        {
            passed,
            timestamp = DateTimeOffset.UtcNow,
            projectEndpoint = endpoint,
            model,
            toolboxName,
            toolboxVersion,
            query = Query,
            answer,
            receipt,
            localCalls,
            events = proof.Events,
        }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Evidence: {Path.Combine(resultsPath, "proof.json")}");
}

[Description("Read the synthetic student snapshot. Load the advising skill first to learn the required skillProof value.")]
StudentSnapshot GetStudentSnapshot(
    [Description("Synthetic student identifier.")] string studentId,
    [Description("Proof value supplied by the loaded advising skill instructions.")] string skillProof)
{
    if (studentId != "demo-001" || skillProof != SkillProof)
    {
        throw new ArgumentException("Invalid synthetic student or skill proof. Load the advising skill before calling this tool.");
    }

    localCalls.Add(new StudentCall(studentId, skillProof, receipt));
    proof.Record("local-function", LocalToolName, new { studentId, skillProof, receipt });
    return new StudentSnapshot(studentId, 93, 120, receipt);
}

async ValueTask<object?> TraceFunctionAsync(
    AIAgent runningAgent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken token)
{
    proof.Record("function-start", context.Function.Name, context.Arguments);
    object? result = await next(context, token);
    proof.Record("function-completed", context.Function.Name, context.Arguments);
    return result;
}

static string Required(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"{name} must be set. See README.md.");

internal sealed record StudentSnapshot(string StudentId, int CompletedCredits, int RequiredCredits, string Receipt);
internal sealed record StudentCall(string StudentId, string SkillProof, string Receipt);
