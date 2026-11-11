# Local .NET agent with a Foundry toolbox skill and local function

An illustrative Microsoft Agent Framework console sample using synthetic data.
The agent and C# function execute locally; Foundry supplies the model and a skill
through a toolbox MCP endpoint. No hosted agent, registered prompt agent, or
Responses hosting adapter is required.

**Sample only.** Review identity, authorization, data handling, error recovery,
and experimental API suitability before adapting it for production. This is not
a university advising policy or a supported production reference architecture.

## Prerequisites

- .NET 10 SDK and Azure CLI.
- An existing Foundry project and a deployed tool-calling model.
- Your signed-in identity must have model-inference and toolbox/skill access.
- Foundry tooling with `azd ai skill` and `azd ai toolbox` if provisioning the
  supplied demo skill. The portal may also be used.
- Access to the exact preview/alpha NuGet packages pinned in the project file.

The sample uses `AzureCliCredential` and an explicitly selected `AZURE_CONFIG_DIR`.
Use a profile appropriate for your own environment. Do not copy someone else's
credential cache. Sign in yourself if the selected profile is not authenticated.

## 1. Restore packages

```powershell
git clone https://github.com/cmw2/maf-foundry-toolbox-skills-dotnet.git
Set-Location maf-foundry-toolbox-skills-dotnet
dotnet restore
```

Dependencies are pinned: MAF Foundry `1.28.0-preview.261106.1`, MAF MCP
`1.28.0-alpha.261106.1`, Azure AI Projects / Extensions OpenAI
`3.0.0-beta.5`, Azure Identity `1.21.0`, MCP `2.2.0`.
Use your organization's approved NuGet feed or package proxy. The author's
environment requires its configured package proxy and cannot connect directly
to NuGet; restore and compilation are verified through that proxy, not a direct
public-feed connection. No internal feed endpoint or credentials are included
in this sample. The MAF version pages are listed on public NuGet, but the
customer's approved feed must carry the pinned versions. Do not silently
substitute versions if a package is unavailable.

## 2. Create the skill and toolbox

The runtime does not read the included local `SKILL.md`. Upload that file to
Foundry and attach it to a toolbox first. These commands create named resources;
run them only in your intended demo project. Do not overwrite an existing
same-named skill or toolbox.

```powershell
$env:AZURE_CONFIG_DIR = 'C:\path\to\your\azure-cli-profile'
$project = 'https://<resource>.services.ai.azure.com/api/projects/<project>'
$env:AZD_AGENT_SKIP_ROLE_ASSIGNMENTS = 'true'

azd ai skill create local-advising-proof `
    --file '.\skills\local-advising-proof\SKILL.md' `
    --project-endpoint $project --no-prompt
if ($LASTEXITCODE -ne 0) { throw 'Skill creation failed.' }

azd ai toolbox create local-advising-proof-toolbox `
    --from-file '.\toolbox.json' `
    --project-endpoint $project --no-prompt
if ($LASTEXITCODE -ne 0) { throw 'Toolbox creation failed.' }
```

`toolbox.json` references skill version `1`. Use the actual toolbox version
returned by creation. No model or role assignment is created by these commands.
If you already have this demo skill in a toolbox, skip creation and provide that
toolbox's name/version when running.

## 3. Run locally

```powershell
.\Run-Demo.ps1 `
    -AzureConfigDir 'C:\path\to\your\azure-cli-profile' `
    -ProjectEndpoint 'https://<resource>.services.ai.azure.com/api/projects/<project>' `
    -Model '<existing-model-deployment-name>'
```

Optional overrides are `-ToolboxName` and `-ToolboxVersion`. Direct `dotnet run`
requires `AZURE_CONFIG_DIR`, `FOUNDRY_PROJECT_ENDPOINT`, `FOUNDRY_MODEL`,
`FOUNDRY_TOOLBOX_NAME`, and `FOUNDRY_TOOLBOX_VERSION`.

The runtime consumes a pinned version:
`{project}/toolboxes/{name}/versions/{version}/mcp?api-version=v1`.
The `api-version=v1` query is the API version, not the toolbox version.

## How it works

```text
Local .NET MAF agent
  |-- model inference --> Foundry model deployment
  |-- UseMcpSkills ----> Foundry toolbox MCP resources
  |                       |-- skill://index.json
  |                       +-- skill://local-advising-proof/SKILL.md
  +-- function call ---> GetStudentSnapshot (local C#)
```

The important wiring in `Program.cs`:

```csharp
using var skillsProvider = new AgentSkillsProviderBuilder()
    .UseMcpSkills(mcpClient)
    .UseFilter((skill, _) => skill.Frontmatter.Name == SkillName)
    .Build();

// In ChatClientAgentOptions:
// ChatOptions.Tools = [AIFunctionFactory.Create(GetStudentSnapshot, ...)]
// AIContextProviders = [skillsProvider]
```

`UseMcpSkills` comes from `Microsoft.Agents.AI.Mcp`, not the hosting package.
Skills are MCP resources; `ListToolsAsync()` alone does not load them.
The provider advertises skill metadata and exposes `load_skill` for on-demand
instruction retrieval. The toolbox contains a skill only; the business function
is registered locally with `AIFunctionFactory`.

The skill directs the model to retrieve a synthetic student's credits, calculate
remaining credits, and return a specific format. A skill is instructions followed
by the model, not Markdown executed by Foundry.

## Observable proof

The sample was run successfully twice against an author's demo Foundry project
using GPT-4.1-mini. No customer data was used. This does not certify a different
customer environment.

```text
[01] resource-read: skill://index.json
[02] function-start: load_skill
[03] resource-read: skill://local-advising-proof/SKILL.md
[04] function-completed: load_skill
[05] function-start: GetStudentSnapshot
[06] local-function: GetStudentSnapshot
[07] function-completed: GetStudentSnapshot
PASS: remote skill loaded, local function invoked, skill instructions followed.
```

Success requires remote resource reads, successful skill loading before the local
function, exactly one valid local call, the skill's output marker, 27 remaining
credits from the tool's 93/120 credit totals, and an unpredictable local receipt
in the answer. Missing evidence throws and exits nonzero.

`results\proof.json` records each run's response and ordered events, excluding
tokens/authorization headers. Each run replaces that file. It includes your
endpoint and configuration, so review it before sharing.

The skill's canary value and marker demonstrate behavior; they are not secrets,
authorization controls, or a security boundary. The runtime never reads the local
skill authoring file. The proof checks know the expected canary but do not inject
it into model context.

Read-only skill operations are auto-approved; script execution is not. This
skill contains no scripts. The skill filter limits agent exposure, not server-side
RBAC. This is a single-user demo, not a multi-user identity/consent implementation.

## Source references

- [Official Step25: toolbox MCP connection](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/02-agents/AgentProviders/foundry/Agent_Step25_FoundryToolboxMcp/Program.cs)
- [Official Step26: toolbox MCP skills](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/02-agents/AgentProviders/foundry/Agent_Step26_FoundryToolboxMcpSkills/Program.cs)
- [Microsoft toolbox-skills sample](https://github.com/microsoft-foundry/foundry-samples/tree/main/samples/csharp/hosted-agents/agent-framework/foundry-toolbox-mcp-skills)
- [MAF MCP package](https://www.nuget.org/packages/Microsoft.Agents.AI.Mcp/1.28.0-alpha.261106.1)
- [MAF Foundry package](https://www.nuget.org/packages/Microsoft.Agents.AI.Foundry/1.28.0-preview.261106.1)

## Disclaimer

This Sample Code is provided for the purpose of illustration only and is not
intended to be used in a production environment. THIS SAMPLE CODE AND ANY RELATED
INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESSED
OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES OF
MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
