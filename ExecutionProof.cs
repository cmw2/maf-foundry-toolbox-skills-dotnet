using System.Text.Json;

internal sealed class ExecutionProof
{
    private readonly List<ProofEvent> _events = [];
    private readonly object _gate = new();

    public ProofEvent[] Events
    {
        get { lock (_gate) { return [.. _events]; } }
    }

    public void Record(string kind, string name, object? data = null)
    {
        lock (_gate)
        {
            _events.Add(new ProofEvent(_events.Count + 1, kind, name,
                JsonSerializer.SerializeToElement(data)));
            Console.WriteLine($"[{_events.Count:00}] {kind}: {name}");
        }
    }

    public void Verify(string skillName, string localToolName, string skillProof,
        string receipt, string answer, IReadOnlyList<StudentCall> localCalls)
    {
        ProofEvent[] events = Events;
        ProofEvent? loaded = events.FirstOrDefault(e =>
            e.Kind == "function-completed" && e.Name == "load_skill" &&
            e.Data.ToString().Contains(skillName, StringComparison.Ordinal));
        ProofEvent? invoked = events.FirstOrDefault(e =>
            e.Kind == "local-function" && e.Name == localToolName);

        Check(events.Any(e => e.Kind == "resource-read" && e.Name == "skill://index.json"),
            "The skill discovery index was not read from the remote toolbox.");
        Check(events.Any(e => e.Kind == "resource-read" &&
            e.Name.StartsWith($"skill://{skillName}/", StringComparison.Ordinal)),
            "The skill content was not read from the remote toolbox.");
        Check(loaded is not null, "The model did not successfully call load_skill for the expected skill.");
        Check(invoked is not null && loaded is not null && invoked.Sequence > loaded.Sequence,
            "The local function was not invoked after loading the skill.");
        Check(localCalls.Count == 1 && localCalls[0].StudentId == "demo-001" &&
            localCalls[0].SkillProof == skillProof && localCalls[0].Receipt == receipt,
            "The local function did not receive exactly one valid, skill-guided call.");
        Check(answer.Contains("SKILL_FROM_FOUNDRY", StringComparison.Ordinal),
            "The answer omitted the marker specified only in the skill body.");
        Check(answer.Contains("Remaining credits: 27", StringComparison.Ordinal),
            "The answer did not apply the skill's calculation to the local tool data.");
        Check(answer.Contains(receipt, StringComparison.Ordinal),
            "The answer did not include the unpredictable receipt returned by the local function.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"PROOF FAILED: {message}");
        }
    }
}

internal sealed record ProofEvent(int Sequence, string Kind, string Name, JsonElement Data);
