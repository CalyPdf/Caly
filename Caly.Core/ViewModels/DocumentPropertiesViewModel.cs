using System.Collections.Generic;

namespace Caly.Core.ViewModels;

public sealed class DocumentPropertiesViewModel
{
    public required string FileName { get; init; }

    public required string FileSize { get; init; }

    public required int PageCount { get; init; }

    /// <summary>
    /// The Pdf version.
    /// </summary>
    public required string PdfVersion { get; init; }

    /// <summary>
    /// The title of this document if applicable.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// The name of the person who created this document if applicable.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// The subject of this document if applicable.
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// Any keywords associated with this document if applicable.
    /// </summary>
    public string? Keywords { get; init; }

    /// <summary>
    /// The name of the application which created the original document before it was converted to PDF if applicable.
    /// </summary>
    public string? Creator { get; init; }

    /// <summary>
    /// The name of the application used to convert the original document to PDF if applicable.
    /// </summary>
    public string? Producer { get; init; }

    /// <summary>
    /// The date and time the document was created.
    /// </summary>
    public string? CreationDate { get; init; }

    /// <summary>
    /// The date and time the document was most recently modified.
    /// </summary>
    public string? ModifiedDate { get; init; }

    /// <summary>
    /// Other information.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Others { get; init; }
}
