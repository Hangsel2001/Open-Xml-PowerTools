﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace OpenXmlPowerTools
{
    class FontMetrics
    {
        private static readonly HashSet<string> UnknownFonts = new HashSet<string>();
        private static HashSet<string> _knownFamilies;

        private static HashSet<string> KnownFamilies
        {
            get
            {
                if (_knownFamilies == null)
                {
                    _knownFamilies = new HashSet<string>();
                    var families = FontFamily.Families;
                    foreach (var fam in families)
                        _knownFamilies.Add(fam.Name);
                }
                return _knownFamilies;
            }
        }

        public static int CalcWidthOfRunInTwips(XElement r, Graphics graphics)
        {
            var paragraph = r.Ancestors(W.p).FirstOrDefault();
            if (paragraph == null)
                return 0;

            var fontName = (string)r.Attribute(PtOpenXml.pt + "FontName") ??
                           (string)paragraph.Attribute(PtOpenXml.pt + "FontName");
            if (fontName == null)
                //throw new OpenXmlPowerToolsException("Internal Error, should have FontName attribute");
                return 0;

            if (UnknownFonts.Contains(fontName))
                return 0;

            var rPr = r.Element(W.rPr);
            if (rPr == null)
                throw new OpenXmlPowerToolsException("Internal Error, should have run properties");

            var sz = GetFontSize(r) ?? 22m;

            // unknown font families will throw ArgumentException, in which case just return 0
            if (!KnownFamilies.Contains(fontName))
                return 0;

            // in theory, all unknown fonts are found by the above test, but if not...
            FontFamily ff;
            try
            {
                ff = new FontFamily(fontName);
            }
            catch (ArgumentException)
            {
                UnknownFonts.Add(fontName);

                return 0;
            }

            var fs = FontStyle.Regular;
            if (GetBoolProp(rPr, W.b) || GetBoolProp(rPr, W.bCs))
                fs |= FontStyle.Bold;
            if (GetBoolProp(rPr, W.i) || GetBoolProp(rPr, W.iCs))
                fs |= FontStyle.Italic;

            // Appended blank as a quick fix to accommodate &nbsp; that will get
            // appended to some layout-critical runs such as list item numbers.
            // In some cases, this might not be required or even wrong, so this
            // must be revisited.
            // TODO: Revisit.
            var runText = r.DescendantsTrimmed(W.txbxContent)
                .Where(e => e.Name == W.t)
                .Select(t => (string)t)
                .StringConcatenate() + " ";

            var tabLength = r.DescendantsTrimmed(W.txbxContent)
                .Where(e => e.Name == W.tab)
                .Select(t => (float)t.Attribute(PtOpenXml.TabWidth))
                .Sum();

            if (runText.Length == 0 && tabLength == 0)
                return 0;

            int multiplier = 1;
            if (runText.Length <= 2)
                multiplier = 100;
            else if (runText.Length <= 4)
                multiplier = 50;
            else if (runText.Length <= 8)
                multiplier = 25;
            else if (runText.Length <= 16)
                multiplier = 12;
            else if (runText.Length <= 32)
                multiplier = 6;
            if (multiplier != 1)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < multiplier; i++)
                    sb.Append(runText);
                runText = sb.ToString();
            }

            using (Font f = new Font(ff, (float)sz / 2f, fs))
            {
                int chars, lines;
                var sf = graphics.MeasureString(runText, f, new SizeF(float.MaxValue, float.MaxValue),
                    StringFormat.GenericTypographic, out chars, out lines);

                // sf returns size in pixels
                const float dpi = 96f;
                var twip = (int)(((sf.Width / dpi) * 1440f) / multiplier + tabLength * 1440f);
                return twip;
            }

        }

        private static bool GetBoolProp(XElement runProps, XName xName)
        {
            var p = runProps.Element(xName);
            if (p == null)
                return false;
            var v = p.Attribute(W.val);
            if (v == null)
                return true;
            var s = v.Value.ToLower();
            if (s == "0" || s == "false")
                return false;
            if (s == "1" || s == "true")
                return true;
            return false;
        }

        public static decimal? GetFontSize(XElement e)
        {
            var languageType = (string)e.Attribute(PtOpenXml.LanguageType);
            if (e.Name == W.p)
            {
                return GetFontSize(languageType, e.Elements(W.pPr).Elements(W.rPr).FirstOrDefault());
            }
            if (e.Name == W.r)
            {
                return GetFontSize(languageType, e.Element(W.rPr));
            }
            return null;
        }

        public static decimal? GetFontSize(string languageType, XElement rPr)
        {
            if (rPr == null) return null;
            return languageType == "bidi"
                ? (decimal?)rPr.Elements(W.szCs).Attributes(W.val).FirstOrDefault()
                : (decimal?)rPr.Elements(W.sz).Attributes(W.val).FirstOrDefault();
        }
    }

    class HtmlToWmlConverter_FontMetrics
    {
        // returns size in points
        public static float MeasureTextRunInPoints(float sz, FontFamily ff, FontStyle fs, string runText, Graphics graphics)
        {
            using (Font f = new Font(ff, (float)sz, fs))
            {
                int chars, lines;
                SizeF sf = graphics.MeasureString(runText, f, new SizeF(float.MaxValue, float.MaxValue),
                    StringFormat.GenericTypographic, out chars, out lines);

                // sf returns size in pixels
                const float dpi = 96f;
                var inches = sf.Width / dpi;
                // 72 points per inch
                var points = inches * 72f;
                return points;
            }
        }

        // returns size in pixels
        public static int? MeasureTextRunInPixels(decimal sz, FontFamily ff, FontStyle fs, string runText, Graphics graphics)
        {
            using (Font f = new Font(ff, (float)sz / 2f, fs))
            {
                int chars, lines;
                var sf = graphics.MeasureString(runText, f, new SizeF(float.MaxValue, float.MaxValue),
                    StringFormat.GenericTypographic, out chars, out lines);
                return (int)sf.Width;
            }
        }
    }
}