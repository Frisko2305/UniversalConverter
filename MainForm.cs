using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UniversalConverter;

public class MainForm : Form
{
    private Button _topLeftRedButton = null!;
    private Label _statusHeaderLabel = null!;
    private readonly Dictionary<FileFormat, Button> _formatButtons = new();
    private readonly Dictionary<FileFormat, Color> _formatColors = new()
    {
        { FileFormat.JPEG,     ColorTranslator.FromHtml("#2563EB") }, // Royal Blue
        { FileFormat.PNG,      ColorTranslator.FromHtml("#10B981") }, // Emerald Green
        { FileFormat.HEIC,     ColorTranslator.FromHtml("#7C3AED") }, // Deep Purple
        { FileFormat.DNG,      ColorTranslator.FromHtml("#D97706") }, // Amber Orange
        { FileFormat.ICO,      ColorTranslator.FromHtml("#0284C7") }, // Sky Blue
        { FileFormat.Word,     ColorTranslator.FromHtml("#1E40AF") }, // Navy Blue
        { FileFormat.PDF,      ColorTranslator.FromHtml("#E11D48") }, // Crimson Coral
        { FileFormat.Markdown, ColorTranslator.FromHtml("#0D9488") }  // Slate Teal
    };

    private string[] _loadedFilePaths = Array.Empty<string>();
    private FileFormat? _loadedFileFormat = null;

    public MainForm()
    {
        InitializeLayout();
        ApplyStep1State();
    }

    private void InitializeLayout()
    {
        Text = "Universal Format Converter";
        Size = new Size(820, 500);
        MinimumSize = new Size(720, 440);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = ColorTranslator.FromHtml("#F8FAFC");

        using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("UniversalConverter.docs.app.ico");
        if (stream != null)
        {
            Icon = new Icon(stream);
        }

        var rootGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(20)
        };
        rootGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
        rootGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var topBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 1,
            ColumnCount = 2
        };
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        _topLeftRedButton = new Button
        {
            Dock = DockStyle.Fill,
            BackColor = ColorTranslator.FromHtml("#DC2626"),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 5, 15, 15)
        };
        _topLeftRedButton.FlatAppearance.BorderSize = 0;
        _topLeftRedButton.Click += OnTopLeftRedButtonClicked;

        _statusHeaderLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = ColorTranslator.FromHtml("#1E293B"),
            Margin = new Padding(0, 0, 0, 10)
        };

        topBar.Controls.Add(_topLeftRedButton, 0, 0);
        topBar.Controls.Add(_statusHeaderLabel, 1, 0);

        var formatGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 4
        };

        for (int c = 0; c < 4; c++)
            formatGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        for (int r = 0; r < 2; r++)
            formatGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        // Row 1: Image Formats
        AddFormatButton(formatGrid, FileFormat.JPEG, "JPEG", 0, 0);
        AddFormatButton(formatGrid, FileFormat.PNG, "PNG", 1, 0);
        AddFormatButton(formatGrid, FileFormat.HEIC, "HEIC", 2, 0);
        AddFormatButton(formatGrid, FileFormat.DNG, "DNG", 3, 0);
        AddFormatButton(formatGrid, FileFormat.ICO, "Icon (.ico)", 0, 1);

        // Row 2: Document Formats
        AddFormatButton(formatGrid, FileFormat.Word, "Word (.docx)", 1, 1);
        AddFormatButton(formatGrid, FileFormat.PDF, "PDF", 2, 1);
        AddFormatButton(formatGrid, FileFormat.Markdown, "Markdown (.md)", 3, 1);

        rootGrid.Controls.Add(topBar, 0, 0);
        rootGrid.Controls.Add(formatGrid, 0, 1);
        Controls.Add(rootGrid);
    }

    private void AddFormatButton(TableLayoutPanel grid, FileFormat format, string label, int col, int row)
    {
        var btn = new Button
        {
            Text = label,
            Dock = DockStyle.Fill,
            Margin = new Padding(10),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = _formatColors[format],
            Cursor = Cursors.Hand,
            Tag = format
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += OnFormatButtonClicked;

        _formatButtons[format] = btn;
        grid.Controls.Add(btn, col, row);
    }

    private void ApplyStep1State()
    {
        _loadedFilePaths = Array.Empty<string>();
        _loadedFileFormat = null;

        _topLeftRedButton.Enabled = true;
        _topLeftRedButton.Text = "Close the app";
        _statusHeaderLabel.Text = "Select the format of the file(s) you want to load";

        foreach (var (format, btn) in _formatButtons)
        {
            btn.Enabled = true;
            btn.BackColor = _formatColors[format];
            btn.ForeColor = Color.White;
            btn.Cursor = Cursors.Hand;
        }
    }

    private void ApplyStep2State(string[] filePaths, FileFormat sourceFormat)
    {
        _loadedFilePaths = filePaths;
        _loadedFileFormat = sourceFormat;

        _topLeftRedButton.Enabled = true;

        if (filePaths.Length == 1)
        {
            string fileNameNoExt = Path.GetFileNameWithoutExtension(filePaths[0]);
            string formatExt = Path.GetExtension(filePaths[0]).TrimStart('.').ToUpperInvariant();
            _topLeftRedButton.Text = "Delete file";
            _statusHeaderLabel.Text = $"\"{fileNameNoExt}\".\"{formatExt}\" is loaded";
        }
        else
        {
            string formatExt = Path.GetExtension(filePaths[0]).TrimStart('.').ToUpperInvariant();
            _topLeftRedButton.Text = "Delete files";
            _statusHeaderLabel.Text = $"{filePaths.Length} \"{formatExt}\" files are loaded";
        }

        foreach (var (targetFormat, btn) in _formatButtons)
        {
            bool allowed = ConversionEngine.CanConvert(sourceFormat, targetFormat);
            btn.Enabled = allowed;

            if (allowed)
            {
                btn.BackColor = _formatColors[targetFormat];
                btn.ForeColor = Color.White;
                btn.Cursor = Cursors.Hand;
            }
            else
            {
                btn.BackColor = ColorTranslator.FromHtml("#CBD5E1");
                btn.ForeColor = ColorTranslator.FromHtml("#64748B");
                btn.Cursor = Cursors.No;
            }
        }
    }

    private void OnTopLeftRedButtonClicked(object? sender, EventArgs e)
    {
        if (_loadedFilePaths.Length == 0)
        {
            Application.Exit();
        }
        else
        {
            ApplyStep1State();
        }
    }

    private async void OnFormatButtonClicked(object? sender, EventArgs e)
    {
        if (sender is not Button clickedButton || clickedButton.Tag is not FileFormat chosenFormat)
            return;

        // STEP 1: Load one or multiple files of the clicked format
        if (_loadedFilePaths.Length == 0 || _loadedFileFormat == null)
        {
            using var openDialog = new OpenFileDialog
            {
                Title = $"Select one or more {chosenFormat} files to load",
                Filter = ConversionEngine.GetFileDialogFilter(chosenFormat),
                Multiselect = true // <-- Enables dragging across multiple files!
            };

            if (openDialog.ShowDialog(this) == DialogResult.OK && openDialog.FileNames.Length > 0)
            {
                ApplyStep2State(openDialog.FileNames, chosenFormat);
            }
            return;
        }

        // STEP 2: Convert the loaded file(s) to the clicked target format
        string[] inputPaths = _loadedFilePaths;
        FileFormat sourceFormat = _loadedFileFormat.Value;
        FileFormat targetFormat = chosenFormat;
        bool isBatch = inputPaths.Length > 1;

        try
        {
            Cursor = Cursors.WaitCursor;
            SetAllButtonsEnabled(false);

            if (!isBatch)
            {
                // Single file conversion
                _statusHeaderLabel.Text = $"Converting to {targetFormat}...";
                byte[] convertedBytes = await Task.Run(() =>
                    ConversionEngine.ConvertFile(inputPaths[0], sourceFormat, targetFormat));

                Cursor = Cursors.Default;

                string targetExt = ConversionEngine.GetExtension(targetFormat);
                string baseName = Path.GetFileNameWithoutExtension(inputPaths[0]);

                using var saveDialog = new SaveFileDialog
                {
                    Title = "Choose where to save your converted file",
                    Filter = ConversionEngine.GetFileDialogFilter(targetFormat),
                    FileName = $"{baseName}.{targetExt}",
                    DefaultExt = targetExt
                };

                if (saveDialog.ShowDialog(this) == DialogResult.OK)
                {
                    await File.WriteAllBytesAsync(saveDialog.FileName, convertedBytes);
                    MessageBox.Show(
                        this,
                        $"File successfully saved to:\n{saveDialog.FileName}",
                        "Conversion Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            else
            {
                // Multi-file conversion -> Automatic .ZIP archive
                var progress = new Progress<(int current, int total)>(p =>
                {
                    _statusHeaderLabel.Text = $"Converting file {p.current}/{p.total} to {targetFormat}...";
                });

                byte[] zipBytes = await Task.Run(() =>
                    ConversionEngine.ConvertFilesToZip(
                        inputPaths,
                        sourceFormat,
                        targetFormat,
                        (curr, tot) => ((IProgress<(int, int)>)progress).Report((curr, tot))));

                Cursor = Cursors.Default;

                string targetExt = ConversionEngine.GetExtension(targetFormat).ToUpperInvariant();
                using var saveDialog = new SaveFileDialog
                {
                    Title = "Choose where to save your compressed ZIP archive",
                    Filter = "ZIP Archive (*.zip)|*.zip",
                    FileName = $"Converted_{targetExt}_Files.zip",
                    DefaultExt = "zip"
                };

                if (saveDialog.ShowDialog(this) == DialogResult.OK)
                {
                    await File.WriteAllBytesAsync(saveDialog.FileName, zipBytes);
                    MessageBox.Show(
                        this,
                        $"{inputPaths.Length} files converted and compressed into:\n{saveDialog.FileName}",
                        "Batch Conversion Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
        }
        catch (Exception ex)
        {
            Cursor = Cursors.Default;
            MessageBox.Show(
                this,
                $"An error occurred during conversion:\n{ex.Message}",
                "Conversion Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            if (_loadedFilePaths.Length > 0 && _loadedFileFormat != null)
            {
                ApplyStep2State(_loadedFilePaths, _loadedFileFormat.Value);
            }
        }
    }

    private void SetAllButtonsEnabled(bool enabled)
    {
        _topLeftRedButton.Enabled = enabled;
        foreach (var btn in _formatButtons.Values)
            btn.Enabled = enabled;
    }
}