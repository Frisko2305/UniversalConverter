using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO.Compression;
using ImageMagick;
using Markdig;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Word = DocumentFormat.OpenXml.Wordprocessing;
using Drawing = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace UniversalConverter;

public enum FileFormat
{
    JPEG,
    PNG,
    HEIC,
    DNG,
    ICO,
    Word,
    PDF,
    Markdown
}

public static class ConversionEngine
{
    /// <summary>
    /// Determines whether converting from sourceFormat to targetFormat is supported.
    /// </summary>
    public static bool CanConvert(FileFormat source, FileFormat target)
    {
        // Rule 1: Same format is always disabled
        if (source == target)
            return false;

        // Rule 3: Cannot convert any format into DNG (RAW sensor input only)
        if (target == FileFormat.DNG)
            return false;

        bool sourceIsImage = IsImage(source);
        bool targetIsImage = IsImage(target);

        // Image -> Image (JPEG, PNG, HEIC)
        if (sourceIsImage && targetIsImage)
            return true;

        // Image -> PDF or Word (Cross-category supported!)
        if (sourceIsImage && (target == FileFormat.PDF || target == FileFormat.Word))
            return true;

        // Document -> Document (Word, PDF, Markdown)
        if (!sourceIsImage && !targetIsImage)
            return true;

        // All other combinations (e.g., Image -> Markdown, Document -> Image) are disabled
        return false;
    }

    public static bool IsImage(FileFormat format) =>
        format is FileFormat.JPEG or FileFormat.PNG or FileFormat.HEIC or FileFormat.DNG or FileFormat.ICO;

    public static string GetExtension(FileFormat format) => format switch
    {
        FileFormat.JPEG => "jpg",
        FileFormat.PNG => "png",
        FileFormat.HEIC => "heic",
        FileFormat.ICO => "ico",
        FileFormat.DNG => "dng",
        FileFormat.Word => "docx",
        FileFormat.PDF => "pdf",
        FileFormat.Markdown => "md",
        _ => "bin"
    };

    public static string GetFileDialogFilter(FileFormat format) => format switch
    {
        FileFormat.JPEG => "JPEG Image (*.jpg;*.jpeg)|*.jpg;*.jpeg",
        FileFormat.PNG => "PNG Image (*.png)|*.png",
        FileFormat.HEIC => "HEIC Image (*.heic)|*.heic",
        FileFormat.DNG => "DNG RAW Image (*.dng)|*.dng",
        FileFormat.Word => "Word Document (*.docx)|*.docx",
        FileFormat.PDF => "PDF Document (*.pdf)|*.pdf",
        FileFormat.Markdown => "Markdown File (*.md;*.markdown)|*.md;*.markdown",
        _ => "All Files (*.*)|*.*"
    };

    /// <summary>
    /// Converts the input file into a byte array of the target format.
    /// </summary>
    public static byte[] ConvertFile(string inputPath, FileFormat source, FileFormat target)
    {
        if (!CanConvert(source, target))
            throw new InvalidOperationException($"Conversion from {source} to {target} is not supported.");

        // 1. Image -> Image
        if (IsImage(source) && IsImage(target))
            return ConvertImageToImage(inputPath, target);

        // 2. Image -> PDF
        if (IsImage(source) && target == FileFormat.PDF)
            return ConvertImageToPdf(inputPath);

        // 3. Image -> Word (.docx)
        if (IsImage(source) && target == FileFormat.Word)
            return ConvertImageToWord(inputPath);

        // 4. Extract plain/structured text from the source document
        string extractedText = source switch
        {
            FileFormat.Markdown => File.ReadAllText(inputPath, Encoding.UTF8),
            FileFormat.Word => ExtractTextFromWord(inputPath),
            FileFormat.PDF => ExtractTextFromPdf(inputPath),
            _ => throw new NotSupportedException()
        };

        // 5. Export to target Document format
        return target switch
        {
            FileFormat.Markdown => ConvertTextToMarkdown(extractedText, source),
            FileFormat.Word => ConvertTextToWord(extractedText),
            FileFormat.PDF => ConvertTextToPdf(extractedText),
            _ => throw new NotSupportedException()
        };
    }

    /// <summary>
    /// Converts multiple files of the same format and packs them into an in-memory .zip archive.
    /// </summary>
    public static byte[] ConvertFilesToZip(
        string[] inputPaths,
        FileFormat source,
        FileFormat target,
        Action<int, int>? onProgress = null)
    {
        if (!CanConvert(source, target))
            throw new InvalidOperationException($"Conversion from {source} to {target} is not supported.");

        string targetExt = GetExtension(target);
        using var zipStream = new MemoryStream();

        using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var usedEntryNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < inputPaths.Length; i++)
            {
                onProgress?.Invoke(i + 1, inputPaths.Length);

                string inputPath = inputPaths[i];
                byte[] convertedBytes = ConvertFile(inputPath, source, target);

                // Ensure unique filenames inside the ZIP (e.g., if photo.jpg and photo.jpeg are both converted to png)
                string baseName = Path.GetFileNameWithoutExtension(inputPath);
                string entryName = $"{baseName}.{targetExt}";
                int counter = 1;
                while (!usedEntryNames.Add(entryName))
                {
                    entryName = $"{baseName}_{counter}.{targetExt}";
                    counter++;
                }

                var zipEntry = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
                using var entryStream = zipEntry.Open();
                entryStream.Write(convertedBytes, 0, convertedBytes.Length);
            }
        }

        return zipStream.ToArray();
    }

    private static byte[] ConvertImageToImage(string inputPath, FileFormat target)
    {
        using var image = new MagickImage(inputPath);

        if (target == FileFormat.ICO)
        {
            // Windows ICO dimensions cannot exceed 256x256
            if (image.Width > 256 || image.Height > 256)
            {
                // Resizes down proportionally to fit inside a 256x256 bounding box
                image.Resize(new MagickGeometry(256, 256) { IgnoreAspectRatio = false });
            }
        }
        
        image.Format = target switch
        {
            FileFormat.JPEG => MagickFormat.Jpeg,
            FileFormat.PNG => MagickFormat.Png,
            FileFormat.HEIC => MagickFormat.Heic,
            FileFormat.ICO => MagickFormat.Ico,
            _ => throw new NotSupportedException()
        };
        return image.ToByteArray();
    }

    private static byte[] ConvertImageToPdf(string inputPath)
    {
        // Normalize any image (including HEIC/DNG) to a PNG stream first for PDFsharp
        using var magickImage = new MagickImage(inputPath);
        magickImage.Format = MagickFormat.Png;
        using var imgStream = new MemoryStream(magickImage.ToByteArray());

        using var document = new PdfDocument();
        PdfPage page = document.AddPage();
        using var xImage = XImage.FromStream(imgStream);

        page.Width = XUnit.FromPoint(xImage.PointWidth);
        page.Height = XUnit.FromPoint(xImage.PointHeight);

        using var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawImage(xImage, 0, 0, page.Width.Point, page.Height.Point);

        using var outStream = new MemoryStream();
        document.Save(outStream, false);
        return outStream.ToArray();
    }

    private static byte[] ConvertImageToWord(string inputPath)
    {
        using var magickImage = new MagickImage(inputPath);
        uint widthPx = magickImage.Width;
        uint heightPx = magickImage.Height;
        magickImage.Format = MagickFormat.Png;
        byte[] pngBytes = magickImage.ToByteArray();

        using var outStream = new MemoryStream();
        using (var wordDoc = WordprocessingDocument.Create(outStream, WordprocessingDocumentType.Document, true))
        {
            MainDocumentPart mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Word.Document(new Word.Body());

            ImagePart imagePart = mainPart.AddImagePart(ImagePartType.Png);
            using (var imgStream = new MemoryStream(pngBytes))
            {
                imagePart.FeedData(imgStream);
            }

            // Scale image to fit nicely within a standard Word page width (max ~6 inches)
            const long emusPerInch = 914400L;
            long maxWidthEmus = 6 * emusPerInch;
            long widthEmus = widthPx * 9525L;
            long heightEmus = heightPx * 9525L;

            if (widthEmus > maxWidthEmus)
            {
                double ratio = (double)maxWidthEmus / widthEmus;
                widthEmus = maxWidthEmus;
                heightEmus = (long)(heightEmus * ratio);
            }

            var element = BuildWordImageElement(mainPart.GetIdOfPart(imagePart), widthEmus, heightEmus);
            mainPart.Document.Body!.AppendChild(new Word.Paragraph(new Word.Run(element)));
            mainPart.Document.Save();
        }
        return outStream.ToArray();
    }

    private static string ExtractTextFromWord(string inputPath)
    {
        var sb = new StringBuilder();
        using var wordDoc = WordprocessingDocument.Open(inputPath, false);
        var body = wordDoc.MainDocumentPart?.Document?.Body;
        if (body == null) return string.Empty;

        foreach (var para in body.Elements<Word.Paragraph>())
        {
            sb.AppendLine(para.InnerText);
        }
        return sb.ToString();
    }

    private static string ExtractTextFromPdf(string inputPath)
    {
        var sb = new StringBuilder();
        using var document = UglyToad.PdfPig.PdfDocument.Open(inputPath);
        foreach (var page in document.GetPages())
        {
            var words = page.GetWords().ToList();
            if (words.Count == 0) continue;

            // Group words into lines by vertical Y position
            var lines = words
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom / 4.0) * 4.0)
                .OrderByDescending(g => g.Key);

            foreach (var line in lines)
            {
                string lineText = string.Join(" ", line.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text));
                sb.AppendLine(lineText);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static byte[] ConvertTextToMarkdown(string text, FileFormat source)
    {
        return Encoding.UTF8.GetBytes(text);
    }

    private static byte[] ConvertTextToWord(string text)
    {
        // Strip basic Markdown symbols if coming from Markdown
        string cleanText = Markdown.ToPlainText(text);

        using var outStream = new MemoryStream();
        using (var wordDoc = WordprocessingDocument.Create(outStream, WordprocessingDocumentType.Document, true))
        {
            MainDocumentPart mainPart = wordDoc.AddMainDocumentPart();
            var body = new Word.Body();

            string[] lines = cleanText.Replace("\r\n", "\n").Split('\n');
            foreach (string line in lines)
            {
                var para = new Word.Paragraph(
                    new Word.Run(
                        new Word.Text(line) { Space = SpaceProcessingModeValues.Preserve }
                    )
                );
                body.AppendChild(para);
            }

            mainPart.Document = new Word.Document(body);
            mainPart.Document.Save();
        }
        return outStream.ToArray();
    }

    private static byte[] ConvertTextToPdf(string text)
    {
        string cleanText = Markdown.ToPlainText(text);

        using var document = new PdfSharp.Pdf.PdfDocument();
        var font = new XFont("Arial", 11, XFontStyleEx.Regular);
        const double margin = 40;
        const double lineHeight = 16;

        string[] paragraphs = cleanText.Replace("\r\n", "\n").Split('\n');

        PdfPage page = document.AddPage();
        XGraphics gfx = XGraphics.FromPdfPage(page);
        double y = margin;
        double maxWidth = page.Width.Point - (margin * 2);

        foreach (string para in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(para))
            {
                y += lineHeight;
                continue;
            }

            string[] words = para.Split(' ');
            var currentLine = new StringBuilder();

            foreach (string word in words)
            {
                string testLine = currentLine.Length == 0 ? word : $"{currentLine} {word}";
                if (gfx.MeasureString(testLine, font).Width > maxWidth)
                {
                    if (y + lineHeight > page.Height.Point - margin)
                    {
                        gfx.Dispose();
                        page = document.AddPage();
                        gfx = XGraphics.FromPdfPage(page);
                        y = margin;
                    }
                    gfx.DrawString(currentLine.ToString(), font, XBrushes.Black, new XRect(margin, y, maxWidth, lineHeight), XStringFormats.TopLeft);
                    y += lineHeight;
                    currentLine.Clear().Append(word);
                }
                else
                {
                    currentLine.Clear().Append(testLine);
                }
            }

            if (currentLine.Length > 0)
            {
                if (y + lineHeight > page.Height.Point - margin)
                {
                    gfx.Dispose();
                    page = document.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    y = margin;
                }
                gfx.DrawString(currentLine.ToString(), font, XBrushes.Black, new XRect(margin, y, maxWidth, lineHeight), XStringFormats.TopLeft);
                y += lineHeight;
            }
        }

        gfx.Dispose();
        using var outStream = new MemoryStream();
        document.Save(outStream, false);
        return outStream.ToArray();
    }

    private static Word.Drawing BuildWordImageElement(string relationshipId, long cx, long cy)
    {
        return new Word.Drawing(
            new DW.Inline(
                new DW.Extent() { Cx = cx, Cy = cy },
                new DW.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties() { Id = 1U, Name = "Embedded Image" },
                new DW.NonVisualGraphicFrameDrawingProperties(new Drawing.GraphicFrameLocks() { NoChangeAspect = true }),
                new Drawing.Graphic(
                    new Drawing.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties() { Id = 0U, Name = "Image.png" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new Drawing.Blip() { Embed = relationshipId },
                                new Drawing.Stretch(new Drawing.FillRectangle())),
                            new PIC.ShapeProperties(
                                new Drawing.Transform2D(
                                    new Drawing.Offset() { X = 0L, Y = 0L },
                                    new Drawing.Extents() { Cx = cx, Cy = cy }),
                                new Drawing.PresetGeometry(new Drawing.AdjustValueList()) { Preset = Drawing.ShapeTypeValues.Rectangle }))
                    ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
            ));
    }
}