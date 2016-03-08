using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;

namespace Microsoft.DbContextPackage
{
    /// <summary>
    /// Interaction logic for Connections.xaml
    /// </summary>
    public partial class Connections : Microsoft.VisualStudio.PlatformUI.DialogWindow
    {
        public string SelectedConnectionString { get; private set; }

        public bool IsConnectionStringSelected
        {
            get
            {
                return !string.IsNullOrWhiteSpace(SelectedConnectionString);
            }
        }

        private Connections()
        {
            InitializeComponent();
            this.HasMaximizeButton = false;
            this.HasMinimizeButton = false;
        }

        public Connections(IEnumerable<string> connectionStrings)
            : this()
        {
            if (connectionStrings == null || !connectionStrings.Any())
            {
                this.SetEmpty();
            }
            else
            {
                this.cmbConnStrings.ItemsSource = connectionStrings;
                this.cmbConnStrings.SelectionChanged += cmbColors_SelectionChanged;
            }
        }

        private void btnCreateNew_Click(object sender, RoutedEventArgs e)
        {
            this.SelectedConnectionString = null;
            this.Close();
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            this.SetValue();
            this.Close();
        }

        private void cmbColors_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            this.SetValue();
        }

        private void SetValue()
        {
            var value = (string)(cmbConnStrings.SelectedItem);
            if (value != null)
            {
                this.SelectedConnectionString = value;
            }
        }

        private void SetEmpty()
        {
            this.cmbConnStrings.Items.Add(new ComboBoxItem { Content = "-- Not Found --" });
            this.cmbConnStrings.IsReadOnly = true;
        }
    }
}