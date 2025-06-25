namespace DependencyUpdates;

using System.Text;
using System.Xml;
using System.Xml.Linq;

public class UpdateableXmlDoc
{
    public string FilePath { get; }
    public XDocument XDocument { get; }

    readonly Encoding readerEncoding;

    UpdateableXmlDoc(string filePath, XDocument xDocument, Encoding readerEncoding)
    {
        FilePath = filePath;
        XDocument = xDocument;
        this.readerEncoding = readerEncoding;
    }

    public static async Task<UpdateableXmlDoc> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(filePath, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);

        var doc = await XDocument.LoadAsync(reader, LoadOptions.PreserveWhitespace, cancellationToken);

        return new UpdateableXmlDoc(filePath, doc, reader.CurrentEncoding);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var writerSettings = new XmlWriterSettings
        {
            Async = true,
            OmitXmlDeclaration = true,
            NewLineHandling = NewLineHandling.None,
            Encoding = readerEncoding
        };

        await using var writer = XmlWriter.Create(FilePath, writerSettings);
        await XDocument.SaveAsync(writer, cancellationToken);
    }
}