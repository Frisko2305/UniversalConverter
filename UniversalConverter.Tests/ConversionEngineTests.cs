using System;
using System.IO;
using System.IO.Compression;
using Xunit;
using UniversalConverter;

namespace UniversalConverter.Tests;

public class ConversionEngineTests
{
    [Theory]
    [InlineData(FileFormat.JPEG, FileFormat.PNG, true)]      // Image to Image allowed
    [InlineData(FileFormat.PNG, FileFormat.PNG, false)]      // Same format disabled
    [InlineData(FileFormat.JPEG, FileFormat.DNG, false)]     // Nothing can convert TO DNG
    [InlineData(FileFormat.Word, FileFormat.PDF, true)]      // Doc to Doc allowed
    [InlineData(FileFormat.JPEG, FileFormat.PDF, true)]      // Cross-category (Image to PDF) allowed
    [InlineData(FileFormat.Word, FileFormat.JPEG, false)]    // Cross-category (Doc to Image) disabled
    public void CanConvert_ReturnsExpectedResult_BasedOnMatrixRules(FileFormat source, FileFormat target, bool expected)
    {
        // Act
        bool result = ConversionEngine.CanConvert(source, target);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertFile_MarkdownToWord_GeneratesValidByteArray()
    {
        // Arrange
        string tempMdPath = Path.Combine(Path.GetTempPath(), "test_document.md");
        File.WriteAllText(tempMdPath, "# Hello World\nThis is a unit test.");

        try
        {
            // Act
            byte[] docxBytes = ConversionEngine.ConvertFile(tempMdPath, FileFormat.Markdown, FileFormat.Word);

            // Assert
            Assert.NotNull(docxBytes);
            Assert.True(docxBytes.Length > 0, "The converted byte array should not be empty.");
            
            // A valid docx file is essentially a zip archive, its header starts with "PK" (50 4B)
            Assert.Equal(0x50, docxBytes[0]);
            Assert.Equal(0x4B, docxBytes[1]);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempMdPath)) File.Delete(tempMdPath);
        }
    }

    [Fact]
    public void ConvertFilesToZip_PacksMultipleFiles_Successfully()
    {
        // Arrange
        string file1 = Path.Combine(Path.GetTempPath(), "test1.md");
        string file2 = Path.Combine(Path.GetTempPath(), "test2.md");
        File.WriteAllText(file1, "Content 1");
        File.WriteAllText(file2, "Content 2");

        try
        {
            // Act
            byte[] zipBytes = ConversionEngine.ConvertFilesToZip(
                new[] { file1, file2 }, 
                FileFormat.Markdown, 
                FileFormat.Word);

            // Assert
            Assert.NotNull(zipBytes);
            Assert.True(zipBytes.Length > 0);

            // Verify the zip contains exactly 2 files
            using var ms = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
            Assert.Equal(2, archive.Entries.Count);
            
            // Verify extensions changed to .docx
            Assert.EndsWith(".docx", archive.Entries[0].FullName);
            Assert.EndsWith(".docx", archive.Entries[1].FullName);
        }
        finally
        {
            // Cleanup
            if (File.Exists(file1)) File.Delete(file1);
            if (File.Exists(file2)) File.Delete(file2);
        }
    }
}