﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace EasyData.Export
{
    /// <summary>
    /// An implementation of <see cref="IDataExporter"/> interface, that performs exporting of the data stream to PDF format
    /// </summary>
    public class PdfDataExporter : IDataExporter
    {
        /// <summary>
        /// Gets the MIME content type of the exporting format.
        /// </summary>
        /// <returns>System.String.</returns>
        public string GetContentType()
        {
            return "application/pdf";
        }

        /// <summary>
        /// Gets the preferred file extension by the exporting format.
        /// </summary>
        /// <returns>A string object that represents the file extension (without the dot)</returns>
        public string GetFileExtension()
        {
            return "pdf";
        }

        /// <summary>
        /// Gets default settings
        /// </summary>
        /// <param name="culture">The culture info</param>
        /// <returns></returns>
        public IDataExportSettings GetDefaultSettings(CultureInfo culture = null)
        {
            return new PdfDataExportSettings(culture);
        }

        /// <summary>
        /// The default settings.
        /// </summary>
        public IDataExportSettings DefaultSettings => PdfDataExportSettings.Default;

        /// <summary>
        /// Gets the current export settings that are passed in the ExportAsync method
        /// </summary>
        protected PdfDataExportSettings ExportSettings { get; private set; }

        /// <summary>
        /// Exports the specified data to the stream.
        /// </summary>
        /// <param name="data">The fetched data.</param>
        /// <param name="stream">The stream.</param>
        public void Export(IEasyDataResultSet data, Stream stream)
        {
            Export(data, stream, PdfDataExportSettings.Default);
        }

        /// <summary>
        /// Exports the specified data to the stream with the specified formats.
        /// </summary>
        /// <param name="data">The fetched data.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="settings">The settings.</param>
        public void Export(IEasyDataResultSet data, Stream stream, IDataExportSettings settings)
        {
            ExportAsync(data, stream, settings)
               .ConfigureAwait(false)
               .GetAwaiter()
               .GetResult();
        }

        /// <summary>
        /// Asynchronous version of <see cref="PdfDataExporter.Export(IEasyDataResultSet,Stream)"/> method.
        /// </summary>
        /// <param name="data">The fetched data.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>Task.</returns>
        public Task ExportAsync(IEasyDataResultSet data, Stream stream, CancellationToken ct = default)
        {
            return ExportAsync(data, stream, PdfDataExportSettings.Default, ct);
        }

        /// <summary>
        /// Asynchronous version of <see cref="PdfDataExporter.Export(IEasyDataResultSet,Stream, IDataExportSettings)" /> method.
        /// </summary>
        /// <param name="data">The fetched data.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="settings">The settings.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task ExportAsync(IEasyDataResultSet data, Stream stream, IDataExportSettings settings, CancellationToken ct = default)
        {
            ExportSettings = MapSettings(settings);

            var document = new Document();
            document.Info.Title = ExportSettings.Title;
            var pageSetup = document.DefaultPageSetup.Clone();
            pageSetup.Orientation = ExportSettings.Orientation;
            pageSetup.PageFormat = ExportSettings.PageFormat;

            ApplyStyles(document);

            var section = document.AddSection();
            section.PageSetup.PageFormat = ExportSettings.PageFormat;
            section.PageSetup.Orientation = ExportSettings.Orientation;
            section.PageSetup.HorizontalPageBreak = true;

            // getting ignored columns
            var ignoredCols = GetIgnoredColumns(data, settings);

            var pageSizes = GetPageSizes(ExportSettings.PageFormat);
            if (ExportSettings.Orientation == Orientation.Landscape) { 
                (pageSizes.Width, pageSizes.Height) = (pageSizes.Height, pageSizes.Width);
            }
            var pageWidth = pageSizes.Width;

            //calculating the width of one column
            var colCount = data.Cols.Count - ignoredCols.Count;
            double pageContentWidth = pageWidth - ExportSettings.Margins.Left - ExportSettings.Margins.Right;
            var colWidth = (int)Math.Ceiling(pageContentWidth / colCount);

            if (ExportSettings.MinColWidth > 0 && colWidth < ExportSettings.MinColWidth) {
                colWidth = ExportSettings.MinColWidth;
                if (ExportSettings.FlexiblePageSize) {
                    pageWidth = colWidth * colCount + ExportSettings.Margins.Left + ExportSettings.Margins.Right;
                    var delta = (double)pageWidth / pageSizes.Width;
                    section.PageSetup.Orientation = Orientation.Portrait;
                    section.PageSetup.PageWidth = Unit.FromMillimeter(pageWidth);
                    section.PageSetup.PageHeight = Unit.FromMillimeter((int)Math.Ceiling(pageSizes.Height*delta));
                }
            }


            if (settings.ShowDatasetInfo) {
                // TODO: render paragraph with info here
                if (!string.IsNullOrWhiteSpace(ExportSettings.Title)) {
                    var p = section.AddParagraph();
                    p.Format.Alignment = ParagraphAlignment.Center;
                    p.Format.Font.Bold = true;
                    p.AddText(ExportSettings.Title);
                }

                if (!string.IsNullOrWhiteSpace(ExportSettings.Description)) {
                    var p = section.AddParagraph();
                    p.Format.Alignment = ParagraphAlignment.Left;
                    p.AddText(ExportSettings.Description);
                }
            }

            section.AddParagraph();

            // Create the item table
            var table = section.AddTable();
            table.Style = "Table";
            table.Borders.Color = Color.FromRgb(0, 0, 0);
            table.Borders.Width = 0.25;
            table.Borders.Left.Width = 0.5;
            table.Borders.Right.Width = 0.5;
            table.Rows.LeftIndent = 0;

            // predefined formatters
            var predefinedFormatters = GetPredefinedFormatters(data.Cols, settings);

            // filling columns
            int colsCount = 0;
            for (int i = 0; i < data.Cols.Count; i++) {
                if (ignoredCols.Contains(i))
                    continue;

                var column = table.AddColumn(Unit.FromMillimeter(colWidth));
                column.Format.Alignment = ParagraphAlignment.Center;
                colsCount++;
            }

            // filling rows
            if (settings.ShowColumnNames) {
                var row = table.AddRow();
                row.HeadingFormat = true;
                row.Format.Alignment = ParagraphAlignment.Center;
                  row.Format.Font.Bold = true;
                row.Shading.Color = Color.FromRgb(0, 191, 255);
                for (int i = 0; i < data.Cols.Count; i++) { 
                    if (ignoredCols.Contains(i))
                        continue;

                    var colName = data.Cols[i].Label;
                    var cell = row.Cells[i];

                    cell.AddParagraph(colName);
                    cell.Format.Font.Bold = false;
                    cell.Format.Alignment = ParagraphAlignment.Center;
                    cell.VerticalAlignment = VerticalAlignment.Center;
                }

                table.SetEdge(0, 0, colsCount, 1, Edge.Box, BorderStyle.Single, 0.75, Color.Empty);
            }

            // filling rows
            var rows = data.Rows.Where(row => {
                var add = settings?.RowFilter?.Invoke(row);
                if (add.HasValue && !add.Value)
                    return false;

                return true;
            }).ToList();


            Task WriteRowAsync(EasyDataRow row, bool isExtra = false, 
                Dictionary<string, object> extraData = null, CancellationToken cancellationToken = default)
            {
                var pdfRow = table.AddRow();
                pdfRow.TopPadding = 1.5;

                for (int i = 0; i < row.Count; i++) {
                    if (ignoredCols.Contains(i)) continue;

                    var col = data.Cols[i];
                    var dfmt = col.DisplayFormat;
                    var gfct = col.GroupFooterColumnTemplate;
                    var type = col.DataType;
                    string value;
                    if (!string.IsNullOrEmpty(dfmt) && predefinedFormatters.TryGetValue(dfmt, out var provider)) {
                        value = string.Format(provider, dfmt, row[i]);
                    }
                    else {
                        value = Utils.GetFormattedValue(row[i], type, ExportSettings, dfmt);
                    }

                    if (!string.IsNullOrEmpty(value) && isExtra && !string.IsNullOrEmpty(gfct)) {
                        value = ExportHelpers.ApplyGroupFooterColumnTemplate(gfct, value, extraData);
                    }

                    value = MapCellValue(value, col);

                    var cell = pdfRow.Cells[i];
                    DrawCell(cell, value, col, isExtra);

                    table.SetEdge(0, 1, colsCount, 1,
                         Edge.Box, BorderStyle.Single, 0.75);
                }

                return Task.CompletedTask;
            }

            WriteRowFunc WriteExtraRowAsync = (extraRow, extraData, cancellationToken) => 
                WriteRowAsync(extraRow, true, extraData, cancellationToken);

            var currentRowNum = 0;
            foreach (var row in rows) {
                if (ExportSettings.BeforeRowInsert != null)
                    await ExportSettings.BeforeRowInsert(row, WriteExtraRowAsync, ct);

                if (settings.RowLimit > 0 && currentRowNum >= settings.RowLimit)
                    continue;

                await WriteRowAsync(row);

                currentRowNum++;
            }

            if (ExportSettings.BeforeRowInsert != null) {
                await ExportSettings.BeforeRowInsert(null, WriteExtraRowAsync, ct);
            }

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            // rendering pdf
            var pdfRenderer = new PdfDocumentRenderer();
            pdfRenderer.Document = document;
            pdfRenderer.RenderDocument();

            using (MemoryStream memoryStream = new MemoryStream()) {
                pdfRenderer.PdfDocument.Save(memoryStream, false);
                memoryStream.Seek(0, SeekOrigin.Begin);

                await memoryStream.CopyToAsync(stream, 4096, ct).ConfigureAwait(false);
            }
        }

        protected virtual string MapCellValue(string value, EasyDataCol column)
        {
            return value;
        }

        protected virtual Paragraph DrawCell(Cell cell, string value, EasyDataCol column, bool isExtra)
        {
            FormatCell(cell, column, isExtra);
            return cell.AddParagraph(value);
        }

        protected virtual void FormatCell(Cell cell, EasyDataCol column, bool isExtra)
        {
            cell.Shading.Color = Color.FromRgb(255, 255, 255);
            cell.VerticalAlignment = VerticalAlignment.Center;
            cell.Format.Alignment = MapAlignment(column.Style.Alignment);
            cell.Format.FirstLineIndent = 1;
            cell.Format.Font.Bold = isExtra;
        }

        private (int Width, int Height) GetPageSizes(PageFormat pageFormat)
        {
            switch (pageFormat) {
                case PageFormat.A0:
                    return (841, 1189);
                case PageFormat.A1:
                    return (594, 841);
                case PageFormat.A2:
                    return (420, 594);
                case PageFormat.A3:
                    return (297, 420);
                case PageFormat.A4:
                    return (210, 297);
                case PageFormat.A5:
                    return (148, 210);
                case PageFormat.A6:
                    return (105, 148);
                case PageFormat.B5:
                    return (176, 250);
                case PageFormat.Letter:
                    return (216, 279);
                case PageFormat.Legal:
                    return (216, 356);
                case PageFormat.Ledger:
                    return (279, 432);
                case PageFormat.P11x17:
                    return (432, 279);
                default:
                    return (210, 297); //return A4 sizes by default
            }
        }

        private Dictionary<string, IFormatProvider> GetPredefinedFormatters(IReadOnlyList<EasyDataCol> cols, IDataExportSettings settings)
        {
            var result = new Dictionary<string, IFormatProvider>();
            for (int i = 0; i < cols.Count; i++) {
                var dfmt = cols[i].DisplayFormat;
                if (!string.IsNullOrEmpty(dfmt) && !result.ContainsKey(dfmt)) {
                    var format = Utils.GetFormat(dfmt);
                    if (format.StartsWith("S")) {
                        result.Add(dfmt, new SequenceFormatter(format, settings.Culture));
                    }
                }

            }
            return result;
        }

        protected static ParagraphAlignment MapAlignment(ColumnAlignment alignment) 
        {
            switch (alignment) {
                case ColumnAlignment.Center:
                    return ParagraphAlignment.Center;
                case ColumnAlignment.Left:
                    return ParagraphAlignment.Left;
                case ColumnAlignment.Right:
                    return ParagraphAlignment.Right;
                default:
                    return ParagraphAlignment.Left;
            }
        }

        /// <summary>
        /// Apply styles for pdf document
        /// </summary>
        /// <param name="document"></param>
        protected virtual void ApplyStyles(Document document)
        {
            // Get the predefined style Normal.
            Style style = document.Styles["Normal"];

            // Because all styles are derived from Normal, the next line changes the 
            // font of the whole document. Or, more exactly, it changes the font of
            // all styles and paragraphs that do not redefine the font.
            style.Font.Name = "Verdana";

            style = document.Styles[StyleNames.Header];
            style.ParagraphFormat.AddTabStop("16cm", TabAlignment.Right);

            style = document.Styles[StyleNames.Footer];
            style.ParagraphFormat.AddTabStop("8cm", TabAlignment.Center);

            // Create a new style called Table based on style Normal
            style = document.Styles.AddStyle("Table", "Normal");
            style.Font.Name = "Verdana";
            style.Font.Name = "Times New Roman";
            style.Font.Size = 9;

            // Create a new style called Reference based on style Normal
            style = document.Styles.AddStyle("Reference", "Normal");
            style.ParagraphFormat.SpaceBefore = "5mm";
            style.ParagraphFormat.SpaceAfter = "5mm";
            style.ParagraphFormat.TabStops.AddTabStop("16cm", TabAlignment.Right);
        }

        private static PdfDataExportSettings MapSettings(IDataExportSettings settings)
        {
            if (settings is PdfDataExportSettings) {
                return settings as PdfDataExportSettings;
            }

            var result = PdfDataExportSettings.Default;
            result.Title = settings.Title;
            result.Description = settings.Description;
            result.ShowDatasetInfo = settings.ShowDatasetInfo;
            result.Culture = settings.Culture;
            result.ShowColumnNames = settings.ShowColumnNames;
            result.RowFilter = settings.RowFilter;
            result.ColumnFilter = settings.ColumnFilter;

            return result;
        }

        private static List<int> GetIgnoredColumns(IEasyDataResultSet data, IDataExportSettings settings)
        {
            var result = new List<int>();
            for (int i = 0; i < data.Cols.Count; i++) {
                var add = settings?.ColumnFilter?.Invoke(data.Cols[i]);
                if (add.HasValue && !add.Value) {
                    result.Add(i);
                }
            }

            return result;
        }
    }
}
