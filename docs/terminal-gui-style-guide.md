
# Terminal GUI Style Guide

This document defines the standard patterns and conventions for creating consistent terminal-based user interfaces in PDConsole and ACUConsole using **Terminal.Gui v2** (2.4.x).

> **Terminal.Gui v2 note.** The consoles were migrated from Terminal.Gui v1 to v2. v2 is a near-complete API rewrite. The most important consequences for dialog code:
> - Widgets moved out of the root `Terminal.Gui` namespace into `Terminal.Gui.App` (application/`MessageBox`), `Terminal.Gui.ViewBase` (`View`, `Pos`, `Dim`), `Terminal.Gui.Views` (all widgets), and `Terminal.Gui.Drawing` (`Color`, `Scheme`, `Attribute`, `LineStyle`).
> - The static `Application` API (`Application.Init/Run/RequestStop/Invoke/Shutdown`) is `[Obsolete]`. Use an injected **`IApplication`** instance instead. Every `Show(...)` takes `IApplication app` as its first parameter.
> - Positional widget constructors were removed. Every widget is created with a parameterless constructor + object initializer.
> - `ComboBox`, `RadioGroup`, `ScrollView`, and `ColorScheme` were removed. Use `DropDownList`, `OptionSelector`, built-in view scrolling, and `Scheme`.

## Table of Contents

1. [Dialog Structure](#dialog-structure)
2. [Layout and Spacing](#layout-and-spacing)
3. [Controls](#controls)
4. [Buttons](#buttons)
5. [Validation and Error Handling](#validation-and-error-handling)
6. [Focus Management](#focus-management)
7. [Multi-Step Dialogs](#multi-step-dialogs)
8. [Result Pattern](#result-pattern)
9. [Code Organization](#code-organization)
10. [Examples](#examples)

---

## Dialog Structure

### Standard Dialog Dimensions

- **Width:** 60 characters (standard for most dialogs). Wider (70–80) for dialogs with long content.
- **Height:** Use **`Dim.Auto()`** so the dialog sizes itself to its content plus border and button row.

> **Always prefer `Height = Dim.Auto()` over a fixed number.** In v2 the `Dialog` button bar lives in the dialog's bottom `Padding` adornment, which is carved out of the content area (v1 drew buttons on the bottom border). A fixed height that "looked right" in v1 clips the last content line in v2. `Dim.Auto()` measures the child views plus the border/padding and can never clip. Reserve a fixed height only for dialogs that intentionally fill a region (e.g. a scrolling text view sized with `Dim.Fill`/`Dim.Percent`).

### Dialog Creation

```csharp
var dialog = new Dialog
{
    Title = "Dialog Title",
    Width = 60,
    Height = Dim.Auto()
};
dialog.Add(/* labels, fields, checkboxes, selectors — NON-button views */);
dialog.AddButton(cancelButton);   // secondary action first
dialog.AddButton(primaryButton);  // primary/default action second
```

- Non-button child views are added with `dialog.Add(...)`.
- Buttons are added with `dialog.AddButton(...)`, in left-to-right display order (`cancel`, then primary).
- After running, dispose the dialog you created: `app.Run(dialog); dialog.Dispose();`

---

## Layout and Spacing

### Coordinate System

Terminal.Gui uses a character-based coordinate system where:
- X axis: horizontal position (columns)
- Y axis: vertical position (rows)
- Origin (0,0) is the top-left of the container's content area (inside the border)

Positions and sizes are set with object-initializer properties. `int` values convert implicitly to `Pos`/`Dim`, so `X = 1` and `Width = 15` are fine.

### Standard Positioning

#### Labels
- **X position:** 1 (left margin with single space padding)
- **Format:** Include the colon in the label text
- **Example:** `new Label { X = 1, Y = 1, Text = "Port:" }`

#### Text Fields and Drop-Down Lists
- **X position:** Align based on the longest label length with **minimum 5 characters spacing** (`x = longest_label_length + 5`):
  - Short labels (up to 10 chars): x = 15
  - Medium labels (11–15 chars): x = 20
  - Long labels (16–20 chars): x = 25
  - Very long labels (21–25 chars): x = 30
  - Extra long labels (26+ chars): x = 31
- **Width:**
  - TextField: 15 (small values), 25, or 35 characters
  - DropDownList: minimum 30 characters so the expanded list displays cleanly; set `Height = 1` for the collapsed input row.

#### CheckBoxes
- **X position:** 1 (left-aligned, full width available for the label)
- **Format:** `new CheckBox { X = 1, Y = y, Text = "Label Text", Value = CheckState.UnChecked }`

### Vertical Spacing

Use consistent Y-coordinate increments:
- **Standard spacing:** Increment by 2 (y = 1, 3, 5, 7, 9…)
- **Tight spacing:** Increment by 1 for related checkboxes
- **Section spacing:** Add an extra row (+2) between logical sections

**Example:**
```csharp
dialog.Add(
    new Label { X = 1, Y = 1, Text = "Field 1:" }, textField1,   // y = 1
    new Label { X = 1, Y = 3, Text = "Field 2:" }, textField2,   // y = 3 (standard)
    new CheckBox { X = 1, Y = 5, Text = "Option 1", Value = CheckState.UnChecked },
    new CheckBox { X = 1, Y = 6, Text = "Option 2", Value = CheckState.Checked },   // y = 6 (tight)
    new Label { X = 1, Y = 8, Text = "Field 3:" }, textField3);  // y = 8 (section)
```

---

## Controls

### TextField

```csharp
var textField = new TextField { X = x, Y = y, Width = width, Text = defaultValue };
```

**Standard Widths:** 15 (small values), 25 (short inputs), 30 (medium), 35 (long).

`TextField.Text` is a plain `string` in v2 — use it directly (no `.ToString()`).

```csharp
// Label "Object ID (hex):" (16 chars) + 5 spacing → x = 21 (use 25)
var hexField = new TextField { X = 25, Y = 1, Width = 15, Text = "5FC105" };
var nameField = new TextField { X = 15, Y = 3, Width = 35, Text = string.Empty };
```

### DropDownList (replaces v1 `ComboBox`)

v1's `ComboBox` was removed. Use `DropDownList`, whose current selection is exposed through its `Text` property. Its items come from a `Source`.

```csharp
var combo = new DropDownList
{
    X = 20, Y = 1, Width = 30, Height = 1,
    Source = new ListWrapper<string>(new ObservableCollection<string>(items))
}.ConfigureForOptimalUX();
```

**Guidelines:**
1. **Minimum width:** 30 characters so the expanded list is not clipped.
2. **`Height = 1`** for the collapsed input row.
3. Call **`.ConfigureForOptimalUX()`** (extension in `{Console}.Extensions`) for consistency.

**Setting the default selection** (by value):
```csharp
combo.Text = items[Math.Max(Array.IndexOf(items, defaultValue), 0)];
```

**Reading the selected index:**
```csharp
var index = Array.IndexOf(items, combo.Text);
var selectedEnum = (SomeEnum)Array.IndexOf(items, combo.Text);
```

**Reacting to selection changes** (v1 `SelectedItemChanged` → v2 `TextChanged`):
```csharp
combo.TextChanged += (_, _) =>
{
    var index = Array.IndexOf(items, combo.Text);
    tempTimeField.Enabled = index >= 2;
};
```

### CheckBox

```csharp
var checkBox = new CheckBox { X = 1, Y = y, Text = "Label Text", Value = CheckState.Checked };
```

- The checked state is the **`Value`** property, of type `CheckState` (`Checked`, `UnChecked`, `None`).
- Reading state: `checkBox.Value == CheckState.Checked`.

```csharp
var useCrcCheckBox = new CheckBox { X = 1, Y = 5, Text = "Use CRC", Value = CheckState.Checked };
// ...
result.UseCrc = useCrcCheckBox.Value == CheckState.Checked;
```

### OptionSelector (replaces v1 `RadioGroup` + `ScrollView`)

v1 wrapped a `RadioGroup` inside a `ScrollView` for single-item selection. In v2, use a single `OptionSelector`.

```csharp
var selector = new OptionSelector
{
    X = 6, Y = 1, Width = 50, Height = 6,
    Labels = deviceList,   // string[] / IReadOnlyList<string>
    Value = 0              // selected index (int?)
};
dialog.Add(selector);
```

- **Read selection:** `selector.Value ?? 0`.
- No `ScrollView`, `RadioGroup`, or `NStack`/`ustring` is needed — `Labels` takes plain strings.

---

## Buttons

### Button Creation and Events

Buttons use object initializers, and their click event is **`Accepting`** (v1 `Clicked` was removed). **Every `Accepting` handler MUST set `e.Handled = true`.**

> **Critical v2 gotcha.** If an `Accepting` handler does not set `e.Handled = true`, the `Accept` command bubbles up the view hierarchy and *also* invokes the dialog's `IsDefault` button. In practice this means a non-default button (e.g. "Browse", "Random") will run its own handler and then additionally trigger the primary button — closing the dialog unexpectedly. Setting `e.Handled = true` stops that propagation.

**Primary Button (default):**
```csharp
var primaryButton = new Button { Text = "Label", IsDefault = true };
primaryButton.Accepting += (_, e) => { PrimaryButtonClicked(); e.Handled = true; };
```
`IsDefault = true` makes this the button activated by the Enter key.

**Secondary Button:**
```csharp
var cancelButton = new Button { Text = "Cancel" };
cancelButton.Accepting += (_, e) => { CancelButtonClicked(); e.Handled = true; };
```

**Non-closing Button (e.g. Browse):**
```csharp
var browseButton = new Button { Text = "Browse" };
browseButton.Accepting += (_, e) => { BrowseButtonClicked(); e.Handled = true; };
```

### Standard Button Labels

| Action | Primary Button | Secondary Button |
|--------|---------------|------------------|
| Connection | "Start" | "Cancel" |
| Configuration | "Apply" / "Update" | "Cancel" |
| Add/Create | "Add" | "Cancel" |
| Multi-step | "Next" | "Cancel" |
| Final step | "Send" / "OK" | "Cancel" |

### Button Order

Add buttons with `AddButton` in left-to-right order — cancel first, primary second:
```csharp
dialog.AddButton(cancelButton);
dialog.AddButton(primaryButton);
```
This displays as: `[Cancel] [Primary]`

### Event Handlers

Define handlers as local functions and invoke them from the `Accepting` lambda:
```csharp
void PrimaryButtonClicked()
{
    if (!ValidateInput())
    {
        MessageBox.ErrorQuery(app, "Error", "Invalid input!", "OK");
        return;
    }

    result.WasCancelled = false;
    app.RequestStop();
}

void CancelButtonClicked()
{
    result.WasCancelled = true;
    app.RequestStop();
}
```

---

## Validation and Error Handling

### Validation Pattern

Validate in the primary button handler before closing the dialog:

```csharp
void PrimaryButtonClicked()
{
    if (!byte.TryParse(addressField.Text, out var address))
    {
        MessageBox.ErrorQuery(app, "Error", "Invalid address entered!", "OK");
        return;
    }

    result.SomeValue = address;
    result.WasCancelled = false;
    app.RequestStop();
}
```

### MessageBox Usage

`MessageBox` lives in `Terminal.Gui.Views` and every overload takes the **`IApplication`** instance as its first argument.

**Error Messages** — note there is **no width/height overload** for `ErrorQuery`:
```csharp
MessageBox.ErrorQuery(app, "Error", "Invalid input!", "OK");
```

**Confirmation Dialogs** — `Query` keeps its optional width/height, but there is **no `defaultButton` index parameter**; the return value is the 0-based index of the selected button:
```csharp
if (MessageBox.Query(app, 60, 10, "Overwrite",
        "Device already exists at that address, overwrite?",
        "No", "Yes") == 0)
{
    return; // User selected "No"
}
```

### Common Validations

```csharp
// Numeric input (Text is a string in v2)
if (!int.TryParse(textField.Text, out var value))
{
    MessageBox.ErrorQuery(app, "Error", "Invalid number entered!", "OK");
    return;
}

// Byte range
if (!byte.TryParse(textField.Text, out var value) || value > 127)
{
    MessageBox.ErrorQuery(app, "Error", "Invalid value entered!", "OK");
    return;
}

// Empty string
if (string.IsNullOrEmpty(textField.Text))
{
    MessageBox.ErrorQuery(app, "Error", "No value entered!", "OK");
    return;
}

// Hex string
try
{
    var bytes = Convert.FromHexString(textField.Text);
}
catch
{
    MessageBox.ErrorQuery(app, "Error", "Invalid hex characters!", "OK");
    return;
}
```

---

## Focus Management

### Initial Focus

Set focus to the first interactive control (typically the first TextField or DropDownList):

```csharp
var nameField = new TextField { X = 15, Y = 1, Width = 35, Text = string.Empty };
var dialog = new Dialog { Title = title, Width = 60, Height = Dim.Auto() };
dialog.Add(new Label { X = 1, Y = 1, Text = "Name:" }, nameField);
dialog.AddButton(cancelButton);
dialog.AddButton(primaryButton);
nameField.SetFocus();

app.Run(dialog);
dialog.Dispose();
```

### Focus for Selection Dialogs

For dialogs whose main control is an `OptionSelector`, set focus to the action button:

```csharp
var sendButton = new Button { Text = "Send", IsDefault = true };
var dialog = new Dialog { Title = title, Width = 60, Height = Dim.Auto() };
dialog.Add(selector);
dialog.AddButton(cancelButton);
dialog.AddButton(sendButton);
sendButton.SetFocus();

app.Run(dialog);
dialog.Dispose();
```

---

## Multi-Step Dialogs

Use multi-step dialogs when collecting complex related data.

### Pattern

1. First dialog collects primary parameters with a "Next" button.
2. After validation, show the second dialog (passing `app` through).
3. If the second dialog completes, collect all data.

**Example:**
```csharp
public static OutputControlInput Show(IApplication app, DeviceSetting[] devices, string[] deviceList)
{
    var result = new OutputControlInput { WasCancelled = true };

    var outputNumberField = new TextField { X = 25, Y = 1, Width = 25, Text = "0" };
    var activateCheckBox = new CheckBox { X = 1, Y = 3, Text = "Activate Output", Value = CheckState.UnChecked };

    void NextButtonClicked()
    {
        if (!byte.TryParse(outputNumberField.Text, out var outputNumber))
        {
            MessageBox.ErrorQuery(app, "Error", "Invalid output number entered!", "OK");
            return;
        }

        app.RequestStop();

        var deviceSelection = DeviceSelectionDialog.Show(app, "Output Control", devices, deviceList);
        if (!deviceSelection.WasCancelled)
        {
            result.OutputNumber = outputNumber;
            result.ActivateOutput = activateCheckBox.Value == CheckState.Checked;
            result.DeviceAddress = deviceSelection.SelectedDeviceAddress;
            result.WasCancelled = false;
        }
    }

    var nextButton = new Button { Text = "Next", IsDefault = true };
    nextButton.Accepting += (_, _) => NextButtonClicked();
    var cancelButton = new Button { Text = "Cancel" };
    cancelButton.Accepting += (_, _) => CancelButtonClicked();

    var dialog = new Dialog { Title = "Output Control", Width = 60, Height = Dim.Auto() };
    dialog.Add(new Label { X = 1, Y = 1, Text = "Output Number:" }, outputNumberField,
              activateCheckBox);
    dialog.AddButton(cancelButton);
    dialog.AddButton(nextButton);
    outputNumberField.SetFocus();

    app.Run(dialog);
    dialog.Dispose();

    return result;
}
```

---

## Result Pattern

### Input Model

All dialogs return an Input model with a `WasCancelled` property:

```csharp
public class SomeDialogInput
{
    public bool WasCancelled { get; set; }
    public string SomeValue { get; set; } = string.Empty;
    public int AnotherValue { get; set; }
}
```

### Dialog Return Pattern

```csharp
public static SomeDialogInput Show(IApplication app, ...)
{
    var result = new SomeDialogInput { WasCancelled = true };

    void PrimaryButtonClicked()
    {
        // Validation...
        result.SomeValue = someField.Text;
        result.AnotherValue = someValue;
        result.WasCancelled = false;
        app.RequestStop();
    }

    void CancelButtonClicked()
    {
        result.WasCancelled = true;
        app.RequestStop();
    }

    // Create and run dialog...
    app.Run(dialog);
    dialog.Dispose();

    return result;
}
```

### Caller Pattern

The view/presenter holds the `IApplication` instance and passes it in:

```csharp
var input = SomeDialog.Show(_app, ...);
if (!input.WasCancelled)
{
    // Use input.SomeValue, input.AnotherValue
}
```

---

## Code Organization

### File Structure

**Dialogs:** `{Console}/Dialogs/{Purpose}Dialog.cs` — one dialog per file.
**Input Models:** `{Console}/Model/DialogInputs/{Purpose}Input.cs` — one model per file.

### Dialog Class Structure

```csharp
using System;
using Terminal.Gui.App;      // IApplication, MessageBox
using Terminal.Gui.ViewBase; // Dim, Pos (for Dim.Auto())
using Terminal.Gui.Views;    // Dialog, Label, Button, TextField, CheckBox, DropDownList, OptionSelector
using {Project}.Configuration;
using {Project}.Model.DialogInputs;

namespace {Project}.Dialogs
{
    /// <summary>
    /// Dialog for collecting {purpose}
    /// </summary>
    public static class {Purpose}Dialog
    {
        /// <summary>
        /// Shows the {purpose} dialog and returns user input
        /// </summary>
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="param1">Description</param>
        /// <returns>{Purpose}Input with user's choices</returns>
        public static {Purpose}Input Show(IApplication app, ...)
        {
            var result = new {Purpose}Input { WasCancelled = true };

            // Control definitions (object initializers)

            // Event handlers as local functions
            void PrimaryButtonClicked() { ... }
            void CancelButtonClicked() { ... }

            // Button definitions (Accepting events)

            // Dialog creation (Height = Dim.Auto()), Add / AddButton

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }

        // Private helper methods (e.g., CreatePortNameDropDown)
    }
}
```

### Consistent Naming

- Controls: `{purpose}TextField`, `{purpose}DropDownList`, `{purpose}CheckBox`, `{purpose}Selector`
- Buttons: `{action}Button` (e.g., `startButton`, `cancelButton`)
- Event handler local functions: `{Action}ButtonClicked`

---

## Examples

### Simple Dialog (Single Step)

```csharp
using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using PDConsole.Configuration;
using PDConsole.Extensions;
using PDConsole.Model.DialogInputs;

namespace PDConsole.Dialogs
{
    /// <summary>
    /// Dialog for configuring serial connection settings
    /// </summary>
    public static class SerialConnectionDialog
    {
        private static readonly string[] StandardBaudRates =
        [
            "9600", "19200", "38400", "57600", "115200", "230400"
        ];

        /// <summary>
        /// Shows the serial connection configuration dialog and returns user input
        /// </summary>
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="currentSettings">Current connection settings for defaults</param>
        /// <returns>SerialConnectionInput with user's choices</returns>
        public static SerialConnectionInput Show(IApplication app, ConnectionSettings currentSettings)
        {
            var result = new SerialConnectionInput { WasCancelled = true };

            var portNameComboBox = CreatePortNameDropDown(15, 1, currentSettings.SerialPortName)
                .ConfigureForOptimalUX();
            var baudRateComboBox = CreateBaudRateDropDown(15, 3, currentSettings.SerialBaudRate)
                .ConfigureForOptimalUX();

            void StartButtonClicked()
            {
                if (string.IsNullOrEmpty(portNameComboBox.Text))
                {
                    MessageBox.ErrorQuery(app, "Error", "No port name selected!", "OK");
                    return;
                }

                if (!int.TryParse(baudRateComboBox.Text, out var baudRate))
                {
                    MessageBox.ErrorQuery(app, "Error", "Invalid baud rate selected!", "OK");
                    return;
                }

                result.PortName = portNameComboBox.Text;
                result.BaudRate = baudRate;
                result.WasCancelled = false;
                app.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                app.RequestStop();
            }

            var startButton = new Button { Text = "Start", IsDefault = true };
            startButton.Accepting += (_, _) => StartButtonClicked();
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, _) => CancelButtonClicked();

            var dialog = new Dialog { Title = "Serial Connection Settings", Width = 60, Height = Dim.Auto() };
            dialog.Add(new Label { X = 1, Y = 1, Text = "Port:" }, portNameComboBox,
                      new Label { X = 1, Y = 3, Text = "Baud Rate:" }, baudRateComboBox);
            dialog.AddButton(cancelButton);
            dialog.AddButton(startButton);
            portNameComboBox.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }

        private static DropDownList CreatePortNameDropDown(int x, int y, string currentPortName)
        {
            var portNames = SerialPort.GetPortNames();
            if (portNames.Length == 0)
            {
                portNames = ["No ports available"];
            }

            var combo = new DropDownList
            {
                X = x, Y = y, Width = 30, Height = 1,
                Source = new ListWrapper<string>(new ObservableCollection<string>(portNames))
            };

            if (!portNames[0].Equals("No ports available"))
            {
                var index = Array.FindIndex(portNames, port =>
                    string.Equals(port, currentPortName, StringComparison.OrdinalIgnoreCase));
                combo.Text = portNames[Math.Max(index, 0)];
            }

            return combo;
        }

        private static DropDownList CreateBaudRateDropDown(int x, int y, int currentBaudRate)
        {
            var combo = new DropDownList
            {
                X = x, Y = y, Width = 30, Height = 1,
                Source = new ListWrapper<string>(new ObservableCollection<string>(StandardBaudRates))
            };

            var index = Array.FindIndex(StandardBaudRates, rate =>
                string.Equals(rate, currentBaudRate.ToString()));
            combo.Text = StandardBaudRates[Math.Max(index, 0)];

            return combo;
        }
    }
}
```

### Selection Dialog (OptionSelector)

```csharp
using System.Linq;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for selecting a device from available devices
    /// </summary>
    public static class DeviceSelectionDialog
    {
        /// <summary>
        /// Shows the device selection dialog and returns user selection
        /// </summary>
        /// <param name="app">The Terminal.Gui application instance driving the dialog.</param>
        /// <param name="title">Dialog title</param>
        /// <param name="devices">Available devices to choose from</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>DeviceSelectionInput with user's choice</returns>
        public static DeviceSelectionInput Show(IApplication app, string title, DeviceSetting[] devices, string[] deviceList)
        {
            var result = new DeviceSelectionInput { WasCancelled = true };

            var deviceSelector = new OptionSelector
            {
                X = 6, Y = 1, Width = 50, Height = 6,
                Labels = deviceList,
                Value = 0
            };

            void SendCommandButtonClicked()
            {
                var selectedDevice = devices.OrderBy(device => device.Address).ToArray()[deviceSelector.Value ?? 0];
                result.SelectedDeviceAddress = selectedDevice.Address;
                result.WasCancelled = false;
                app.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                app.RequestStop();
            }

            var sendButton = new Button { Text = "Send", IsDefault = true };
            sendButton.Accepting += (_, _) => SendCommandButtonClicked();
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, _) => CancelButtonClicked();

            var dialog = new Dialog { Title = title, Width = 60, Height = Dim.Auto() };
            dialog.Add(deviceSelector);
            dialog.AddButton(cancelButton);
            dialog.AddButton(sendButton);
            sendButton.SetFocus();

            app.Run(dialog);
            dialog.Dispose();

            return result;
        }
    }
}
```

---

## Summary Checklist

When creating a new dialog, ensure:

- [ ] `Show(...)` takes `IApplication app` as its first parameter
- [ ] Widgets created with object initializers (no positional constructors)
- [ ] Namespaces: `Terminal.Gui.App`, `Terminal.Gui.ViewBase`, `Terminal.Gui.Views` (no root `Terminal.Gui`)
- [ ] Dialog width is 60 (standard); wider only when content requires it
- [ ] **Dialog height is `Dim.Auto()`** (never a hand-tuned fixed number, unless intentionally filling a region)
- [ ] Labels positioned at x=1 with a colon in the text
- [ ] Controls aligned consistently (x=15, 20, 25, …)
- [ ] Vertical spacing uses y increment of 2 (or 1 for related items)
- [ ] DropDownList width is at least 30 and uses `.ConfigureForOptimalUX()`
- [ ] DropDownList `Source` is a `ListWrapper<string>` over an `ObservableCollection<string>`; selection read via `.Text` / `Array.IndexOf`
- [ ] Single-choice lists use `OptionSelector` (`Labels` + `Value`), not `RadioGroup`/`ScrollView`
- [ ] CheckBox state uses `Value` / `CheckState`
- [ ] Buttons use object initializers and the `Accepting` event (not `Clicked`); primary is `IsDefault = true`
- [ ] Buttons added via `AddButton` in order: cancel, then primary
- [ ] `MessageBox.*` calls pass `app` first; `ErrorQuery` has no width/height; `Query` has no defaultButton index
- [ ] Event handlers are local functions
- [ ] Validation occurs before closing the dialog
- [ ] Result pattern uses the `WasCancelled` property
- [ ] First control (or action button for selection dialogs) receives focus
- [ ] `app.Run(dialog)` is followed by `dialog.Dispose()`
- [ ] Static class with static `Show()` method; XML docs on class and method
- [ ] File in the correct location (`Dialogs/` or `Model/DialogInputs/`)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-10-27 | Initial version based on PDConsole and ACUConsole patterns (Terminal.Gui v1) |
| 2.0 | 2026-07-17 | Rewritten for Terminal.Gui v2: instance `IApplication`, object initializers, `Dim.Auto()` heights, `DropDownList`, `OptionSelector`, `CheckState`, `Accepting` events, updated `MessageBox` signatures and namespaces |
