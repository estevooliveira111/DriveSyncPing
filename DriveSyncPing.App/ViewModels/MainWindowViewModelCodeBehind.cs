// Just making sure the combobox updates correctly in MainWindow code-behind if needed, but two-way binding on SelectedItem is default.
// Actually, Avalonia doesn't update the property back without Mode=TwoWay or a setter. Let's patch the XAML to Mode=TwoWay.
