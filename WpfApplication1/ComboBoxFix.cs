using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VineCorrupt
{
    public static class ComboBoxFix
    {
        private static bool _isInitialized = false;

        /// <summary>
        /// Initialize must be called 
        /// before any Combo boxes are created
        /// </summary>
        public static void Initialize()
        {
            if (!_isInitialized)
            {
                // Registed the callback methods 
                // when the properties change on a ComboBox class
                ComboBox.BackgroundProperty.OverrideMetadata(
                    typeof(ComboBox),
                    new FrameworkPropertyMetadata(OnBackgroundChanged));

                ComboBox.ForegroundProperty.OverrideMetadata(
                    typeof(ComboBox),
                    new FrameworkPropertyMetadata(OnForegroundChanged));

                _isInitialized = true;
            }
        }

        private static void OnBackgroundChanged(
          DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Set the drop down background color to match the background
            SetDropDownBackground(d as ComboBox);
        }

        private static void OnForegroundChanged(
          DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Manually set the foreground (to overcome bug)
            // Apparently the ComboBox does not listen 
            // when the Foreground DepencyProperty
            // is changed and therefore does not 
            // update itself unless the value is changed 
            // through the Foreground .net property
            (d as ComboBox).Foreground = e.NewValue as Brush;
        }

        private static void SetDropDownBackground(ComboBox comboBox)
        {
            // The drop down control uses 
            // the WindowBrush to paint its background
            // By overriding that Brush (just for this control)

            if (comboBox.Resources.Contains(SystemColors.WindowBrushKey))
            {
                comboBox.Resources.Remove(SystemColors.WindowBrushKey);
            }

            comboBox.Resources.Add(
              SystemColors.WindowBrushKey, comboBox.Background);
        }
    }
}
