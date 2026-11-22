namespace Theseus.Core.Markdown;

/// <summary>The Markdown box's toolbar buttons (1.1e AC 22).</summary>
public enum MarkdownAction
{
    /// <summary>Bold: **text** (Ctrl+B).</summary>
    Bold,

    /// <summary>Italic: *text* (Ctrl+I).</summary>
    Italic,

    /// <summary>A heading: ## at the start of the line.</summary>
    Heading,

    /// <summary>A bulleted list: - at the start of each line.</summary>
    BulletedList,

    /// <summary>A numbered list: 1. 2. 3. at the start of each line.</summary>
    NumberedList,

    /// <summary>A check box: - [ ] at the start of each line.</summary>
    CheckBox,

    /// <summary>Code: `text` on one line, a fenced block over several.</summary>
    Code,

    /// <summary>A link: [text](address) (Ctrl+K).</summary>
    Link,
}
