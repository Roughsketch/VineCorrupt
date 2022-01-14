using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VineCorrupt
{
    /// <summary>
    /// Interaction logic for WinHotkeys.xaml
    /// </summary>
    public partial class WinHotkeys : Window
    {
        public WinHotkeys()
        {
            InitializeComponent();

            var hotkeys = JsonConvert.DeserializeObject<Dictionary<string, string>>(Properties.Settings.Default.Hotkeys);

            txtCorrupt.Text = hotkeys["corrupt"];
            txtIncStep.Text = hotkeys["inc_step"];
            txtDecStep.Text = hotkeys["dec_step"];
            txtIncValue.Text = hotkeys["inc_value"];
            txtDecValue.Text = hotkeys["dec_value"];
            txtChangeCorruption.Text = hotkeys["change_corruption"];
        }

        public Dictionary<string, string> GetHotkeys()
        {
            Dictionary<string, string> hotkeys = new Dictionary<string, string>();
            
            hotkeys["corrupt"] = txtCorrupt.Text;
            hotkeys["inc_step"] = txtIncStep.Text;
            hotkeys["dec_step"] = txtDecStep.Text;
            hotkeys["inc_value"] = txtIncValue.Text;
            hotkeys["dec_value"] = txtDecValue.Text;
            hotkeys["change_corruption"] = txtChangeCorruption.Text;

            return hotkeys;
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            TextBox textbox = sender as TextBox;

            textbox.Text = "";

            if ((Keyboard.Modifiers & ModifierKeys.Alt) > 0)
            {
                textbox.Text = "Alt";
            }

            if ((Keyboard.Modifiers & ModifierKeys.Control) > 0)
            {
                if (textbox.Text != "")
                {
                    textbox.Text += " + Ctrl";
                }
                else
                {
                    textbox.Text = "Ctrl";
                }
            }

            if ((Keyboard.Modifiers & ModifierKeys.Shift) > 0)
            {
                if (textbox.Text != "")
                {
                    textbox.Text += " + Shift";
                }
                else
                {
                    textbox.Text = "Shift";
                }
            }

            if ((Keyboard.Modifiers & ModifierKeys.Windows) > 0)
            {
                if (textbox.Text != "")
                {
                    textbox.Text += " + Win";
                }
                else
                {
                    textbox.Text = "Win";
                }
            }

            if (textbox.Text != "")
            {
                textbox.Text += " + " + e.Key.ToString();
            }
            else
            {
                textbox.Text = e.Key.ToString();
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox control = sender as TextBox;
            string text = control.Text;
            try
            {
                Hotkey hk = new Hotkey(text);

                int count = 0;

                if (txtCorrupt.Text == text)
                {
                    count++;
                }

                if (txtIncStep.Text == text)
                {
                    count++;
                }

                if (txtDecStep.Text == text)
                {
                    count++;
                }

                if (txtIncValue.Text == text)
                {
                    count++;
                }

                if (txtDecValue.Text == text)
                {
                    count++;
                }

                if (txtChangeCorruption.Text == text)
                {
                    count++;
                }

                if (count > 1)
                {
                    MessageBox.Show("Cannot assign the same hotkey twice.");
                    control.Text = "";
                }
            }
            catch
            {
                MessageBox.Show("Invalid hotkey: " + text);
                control.Text = "";
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This section is currently a work in progress and as such certain combinations of hotkeys may not work. You will be told if they don't work when clicking out of the textbox." +
                "\n\nHotkeys will be saved when you close the window and will persist through exiting the application." +
                "\n\nHotkeys will currently not work on the NES tab.");
        }
    }
}
