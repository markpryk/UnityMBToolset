using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BDT.GUI.Helpers
{
    public static class UIColors
    {
        private static readonly Color _colorGrayLine;
        private static readonly Color _colorYellow;
        private static readonly Color _colorGreen;
        private static readonly Color _colorOrange;
        private static readonly Color _colorRed;
        private static readonly Color _colorDisable;
        private static readonly Color _colorVividCerulean;
        private static readonly Color _colorStandartText;
        private static readonly Color _colorCyan;
        private static readonly Color _colorWheat;
        private static readonly Color _colorRose;
        

        static UIColors()
        {
            ColorUtility.TryParseHtmlString("#000000", out _colorStandartText);

            ColorUtility.TryParseHtmlString("#52565e", out _colorGrayLine);
            ColorUtility.TryParseHtmlString("#fbb034", out _colorYellow);
            ColorUtility.TryParseHtmlString("#66ff66", out _colorGreen);
            ColorUtility.TryParseHtmlString("#ff9900", out _colorOrange);
            ColorUtility.TryParseHtmlString("#f50537", out _colorRed);
            ColorUtility.TryParseHtmlString("#caccd1", out _colorDisable);  
            ColorUtility.TryParseHtmlString("#0dd3ff", out _colorVividCerulean);
            ColorUtility.TryParseHtmlString("#00b2a9", out _colorCyan);
            ColorUtility.TryParseHtmlString("#f9e498", out _colorWheat);
            ColorUtility.TryParseHtmlString("#f7c8c9", out _colorRose);
        }

        public static Color Yellow => _colorYellow;
        public static Color Green => _colorGreen;
        public static Color Orange => _colorOrange;
        public static Color GrayLine => _colorGrayLine;
        public static Color Red => _colorRed;
        public static Color Disable => _colorDisable;
        public static Color VividCerulean => _colorVividCerulean;
        public static Color Cyan => _colorCyan;
        public static Color Wheat => _colorWheat;
        public static Color Rose => _colorRose;
        public static Color StandartText => _colorStandartText;
    }
}