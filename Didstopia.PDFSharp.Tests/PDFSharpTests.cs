using System;
using System.IO;
using Didstopia.PDFSharp.Drawing;
using Didstopia.PDFSharp.Fonts;
using Didstopia.PDFSharp.Pdf;
using Didstopia.PDFSharp.Pdf.AcroForms;
using Didstopia.PDFSharp.Pdf.IO;
using MigraDocCore.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes;
using Xunit;
using Xunit.Abstractions;

namespace Didstopia.PDFSharp.Tests
{
    public class PDFSharpTests
    {
        // TODO: Implement more tests, since these are VERY barebones
        //       and not in any way reliable as is

        private const string TitleString = "Unit Test PDF";
        private const string FontName = "Tinos";
        private const int FontSize = 16;

        private const string PasswordSamplePath = "Samples/pdf_sample_password.pdf";
        private const string PasswordSamplePath2 = "Samples/pdf-example-password.original.pdf";
        private const string PasswordSamplePathAcroForm = "Samples/pdf-example-acroform.pdf";

        private readonly ITestOutputHelper output;

        public PDFSharpTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void TestBasicFunctionality()
        {
            // Create a new PDF
            var pdfDocument = new PdfDocument();
            Assert.True(pdfDocument != null, "PDF should not be null");

            // Set the PDF title
            pdfDocument.Info.Title = TitleString;
            Assert.True(pdfDocument.Info.Title.Equals(TitleString), $"PDF title should equal { TitleString }");

            // Add a new page to the PDF
            PdfPage pdfPage = pdfDocument.AddPage();
            Assert.True(pdfPage != null, "PDF page should not be null");

            // Draw text on the newly created page
            if (GlobalFontSettings.FontResolver == null)
            {
                GlobalFontSettings.FontResolver = new FontResolver();
            }
            XGraphics pdfGraphics = XGraphics.FromPdfPage(pdfPage);
            XFont pdfFont = new XFont(FontName, FontSize, XFontStyle.Regular);
            pdfGraphics.DrawString(TitleString, pdfFont, XBrushes.Black, new XRect(0, 0, pdfPage.Width, pdfPage.Height), XStringFormats.Center);

            // Add an image with transparency
            ImageSource.ImageSourceImpl = new ImageSharpImageSource();
            pdfGraphics.DrawImage(XImage.FromFile("Samples/sample.png"), new XRect(0, 0, pdfPage.Width, pdfPage.Height));

            // Save the PDF to a temporary path
            var tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pdf");
            Assert.False(File.Exists(tempFilePath), "Temporary file should not exist before saving");
            Assert.True(pdfDocument.CanSave(ref tempFilePath), $"PDF should be able to be saved to path: { tempFilePath }");
            pdfDocument.Save(tempFilePath);
            Assert.True(File.Exists(tempFilePath), "Temporary file should exist after saving");
            var tempFileLength = new FileInfo(tempFilePath).Length;
            Assert.True(tempFileLength > 0, $"Temporary file length should be greater than 0 after saving, but was { tempFileLength } instead");

            // Close and dispose of the saved PDF
            pdfDocument.Close();
            pdfDocument.Dispose();

            // Load the PDF from the temporary path
            var loadedPdfDocument = PdfReader.Open(tempFilePath, PdfDocumentOpenMode.ReadOnly);
            Assert.True(loadedPdfDocument != null, "PDF should not be null");
            Assert.True(loadedPdfDocument.Info != null, "PDF info should not be null");
            Assert.True(loadedPdfDocument.Info.Title.Equals(TitleString), $"PDF title should equal { TitleString }");

            // Close and dispose of the loaded PDF
            loadedPdfDocument.Close();
            loadedPdfDocument.Dispose();
        }

        /// <summary>
        /// Ensure opening documents with null provider, or invalid password fail predicably
        /// </summary>
        [Fact()]
        public void TestEncryptionFailsProperly()
        {
            // attempt open with null provider, should not throw exception
            PdfReader.Open(PasswordSamplePath, PdfDocumentOpenMode.ReadOnly, null);

            // purposely open document with invalid password, ensure pdf reader exception thrown
            Assert.Throws<PdfReaderException>(() =>
            {
                PdfReader.Open(PasswordSamplePath, "invalid password", PdfDocumentOpenMode.ReadOnly);
            });

            // assert that message indicating password is invalid
            try
            {
                PdfReader.Open(PasswordSamplePath, "invalid password", PdfDocumentOpenMode.ReadOnly);
            }

            catch (Exception ex)
            {
                Assert.Equal("The specified password is invalid.", ex.Message);
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// Ensure opening a secure document with appropriate password succeeds
        /// </summary>
        [Fact()]
        public void TestEncryption()
        {
            // Load the PDF (using other password protected doc as password didnt seem to be the password?)
            var pdfDocument = PdfReader.Open(PasswordSamplePath2, "test", PdfDocumentOpenMode.ReadOnly);
            Assert.True(pdfDocument != null, "PDF should not be null");
            Assert.True(pdfDocument.Info != null, "PDF info should not be null");

            // Title changed here, and this sample has garbledegoup title, so maybe additional other doc should be used
            // Assert.True(pdfDocument.Info.Title.Equals(TitleString), $"PDF title should equal { TitleString }");

            // Close and dispose of the loaded PDF
            pdfDocument.Close();
            pdfDocument.Dispose();
        }

        /// <summary>
        /// Ensure AcroForm functionality operates as expected
        /// </summary>
        [Fact()]
        public void TextAcroForms()
        {
            // Set the global font resolver
            if (GlobalFontSettings.FontResolver == null)
            {
                GlobalFontSettings.FontResolver = new FontResolver();
            }

            // Load the PDF
            var pdfDocument = PdfReader.Open(PasswordSamplePathAcroForm, PdfDocumentOpenMode.Modify);
            Assert.True(pdfDocument != null, "PDF should not be null");
            Assert.True(pdfDocument.Info != null, "PDF info should not be null");

            var acroForm = pdfDocument.AcroForm;
            Assert.NotNull(acroForm);

            // Enable AcroForm
            if (acroForm.Elements.ContainsKey("/NeedAppearances"))
            {
                acroForm.Elements["/NeedAppearances"] = new PdfBoolean(true);
            }
            else
            {
                acroForm.Elements.Add("/NeedAppearances", new PdfBoolean(true));
            }

            // Update a field
            ((PdfTextField)(acroForm.Fields["Here we have a form field"])).Value = new PdfString("Success");

            // Save the filled-in PDF form to a temporary path
            var tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pdf");
            // Output the path for manual verification of result
            output.WriteLine($"Temp path: {tempFilePath}");
            Assert.False(File.Exists(tempFilePath), "Temporary file should not exist before saving");
            Assert.True(pdfDocument.CanSave(ref tempFilePath), $"PDF should be able to be saved to path: { tempFilePath }");
            pdfDocument.Save(tempFilePath);
            Assert.True(File.Exists(tempFilePath), "Temporary file should exist after saving");
            var tempFileLength = new FileInfo(tempFilePath).Length;
            Assert.True(tempFileLength > 0, $"Temporary file length should be greater than 0 after saving, but was { tempFileLength } instead");

            // Close and dispose of the loaded PDF
            pdfDocument.Close();
            pdfDocument.Dispose();
        }
    }
}
