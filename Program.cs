using System;
using System.Windows.Forms;
using PdfSharp;
using PdfSharp.Fonts;

namespace UniversalConverter;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        try
        {
            PdfSharpLicense.SetAgplLicense();

        // 2. Allow PDFsharp to load Windows fonts (Arial) for Word/MD -> PDF
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        // To customize application configuration such as set high DPI settings or default font,
        // 1. Load the free open-source license required by PDFsharp 6.5+
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }    
}