namespace Theseus.Core.Projects;

/// <summary>The pipelines a project objective's workstreams can run (method/pipelines).</summary>
public enum PipelineKind
{
    /// <summary>research, spec, plan, implement, verify, review, compound.</summary>
    Feature,

    /// <summary>reproduce, rule, fix, verify, then review and compound when the rule stage ran.</summary>
    Bug,

    /// <summary>Ad hoc work: one stage, no gates.</summary>
    Freeform,
}
