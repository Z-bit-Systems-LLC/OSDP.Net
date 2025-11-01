# Terminal GUI Style Guide

This document defines the standard patterns and conventions for creating consistent terminal-based user interfaces in PDConsole and ACUConsole using Terminal.Gui.

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

- **Width:** 60 characters (standard for most dialogs)
- **Height:** Varies based on content
  - Simple forms (2-3 fields): 10 lines
  - Medium forms (4-5 fields): 13 lines
  - Complex forms or lists: 15+ lines

### Dialog Constructor

```csharp
var dialog = new Dialog(title, width, height, cancelButton, primaryButton);
```

**Parameter Order:**
1. Title string
2. Width (typically 60)
3. Height (10, 13, 15, etc.)
4. Cancel button (secondary action)
5. Primary button (default action)

---

## Layout and Spacing

### Coordinate System

Terminal.Gui uses a character-based coordinate system where:
- X axis: horizontal position (columns)
- Y axis: vertical position (rows)
- Origin (0,0) is top-left of container

### Standard Positioning

#### Labels
- **X position:** 1 (left margin with single space padding)
- **Format:** Include colon in label text
- **Example:** `new Label(1, 1, "Port:")`

#### Text Fields and ComboBoxes
- **X position:** Align based on longest label length
  - Short labels (4-6 chars): x = 15
  - Medium labels (7-12 chars): x = 20
  - Long labels (13-18 chars): x = 25
- **Width:**
  - TextField: 25, 30, or 35 characters
  - ComboBox: Minimum 30 characters (required for dropdown)

#### CheckBoxes
- **X position:** 1 (left-aligned, full width available for label)
- **Format:** `new CheckBox(1, y, "Label Text", defaultChecked)`

### Vertical Spacing

Use consistent Y-coordinate increments:
- **Standard spacing:** Increment by 2 (y = 1, 3, 5, 7, 9...)
- **Tight spacing:** Increment by 1 for related checkboxes
- **Section spacing:** Add extra row (+2) between logical sections

**Example:**
```csharp
// Field at y=1
new Label(1, 1, "Field 1:"), textField1,
// Field at y=3 (standard spacing)
new Label(1, 3, "Field 2:"), textField2,
// CheckBox at y=5 (standard spacing)
new CheckBox(1, 5, "Option 1", false),
// Related CheckBox at y=6 (tight spacing)
new CheckBox(1, 6, "Option 2", true),
// Next field at y=8 (section spacing)
new Label(1, 8, "Field 3:"), textField3
```

---

## Controls

### TextField

**Format:**
```csharp
var textField = new TextField(x, y, width, defaultValue);
```

**Standard Widths:**
- 25 characters: Short inputs (numbers, codes)
- 30 characters: Medium inputs (names, single-line text)
- 35 characters: Long inputs (paths, descriptions)

**Example:**
```csharp
var nameField = new TextField(15, 1, 35, string.Empty);
var portField = new TextField(20, 3, 30, "9600");
```

### ComboBox

**Format:**
```csharp
var comboBox = new ComboBox(new Rect(x, y, width, 5), items)
    .ConfigureForOptimalUX();
```

**Critical Requirements:**
1. **Minimum width:** 30 characters (enforced by extension method)
2. **MUST use** `.ConfigureForOptimalUX()` extension method
3. **Height parameter:** Always use 5 for dropdown display

**Width Explanation:**
The width parameter in `new Rect(x, y, width, 5)` determines the dropdown list display area, not just the input box. Using width less than 30 causes dropdown clipping.

**Example:**
```csharp
// CORRECT
var portComboBox = new ComboBox(new Rect(20, 1, 30, 5), portNames)
    .ConfigureForOptimalUX();

// INCORRECT - Will throw exception
var portComboBox = new ComboBox(new Rect(20, 1, 20, 5), portNames)
    .ConfigureForOptimalUX();
```

**Setting Default Selection:**
```csharp
comboBox.SelectedItem = Math.Max(
    Array.FindIndex(items, item => item == defaultValue),
    0);
```

### CheckBox

**Format:**
```csharp
var checkBox = new CheckBox(x, y, "Label Text", defaultChecked);
```

**Guidelines:**
- Use x=1 for left alignment
- Label should be descriptive and action-oriented
- Use tight spacing (y increment by 1) for related checkboxes

**Example:**
```csharp
var useCrcCheckBox = new CheckBox(1, 5, "Use CRC", true);
var useSecureChannelCheckBox = new CheckBox(1, 6, "Use Secure Channel", true);
```

### RadioGroup with ScrollView

Use for selecting one item from a list, especially when list may exceed visible area.

**Format:**
```csharp
var scrollView = new ScrollView(new Rect(x, y, width, height))
{
    ContentSize = new Size(contentWidth, items.Length * 2),
    ShowVerticalScrollIndicator = items.Length > visibleItems,
    ShowHorizontalScrollIndicator = false
};

var radioGroup = new RadioGroup(0, 0, items)
{
    SelectedItem = 0
};
scrollView.Add(radioGroup);
dialog.Add(scrollView);
```

**Guidelines:**
- Use when more than 6 items (show scroll indicator)
- Width: 50 characters for device lists
- Height: 6 rows visible
- ContentSize height: `items.Length * 2` (accounts for spacing)
- Position at x=6 for slight indent

**Example:**
```csharp
var scrollView = new ScrollView(new Rect(6, 1, 50, 6))
{
    ContentSize = new Size(40, deviceList.Length * 2),
    ShowVerticalScrollIndicator = deviceList.Length > 6,
    ShowHorizontalScrollIndicator = false
};

var deviceRadioGroup = new RadioGroup(0, 0, deviceList.Select(ustring.Make).ToArray())
{
    SelectedItem = 0
};
scrollView.Add(deviceRadioGroup);
```

---

## Buttons

### Button Types

**Primary Button (Default):**
```csharp
var primaryButton = new Button("Label", true);
primaryButton.Clicked += PrimaryButtonClicked;
```
The `true` parameter makes this the default button (activated by Enter key).

**Secondary Button:**
```csharp
var cancelButton = new Button("Cancel");
cancelButton.Clicked += CancelButtonClicked;
```

### Standard Button Labels

| Action | Primary Button | Secondary Button |
|--------|---------------|------------------|
| Connection | "Start" | "Cancel" |
| Configuration | "Apply" | "Cancel" |
| Add/Create | "Add" | "Cancel" |
| Multi-step | "Next" | "Cancel" |
| Final step | "Send" / "OK" | "Cancel" |

### Button Order

In Dialog constructor, buttons appear right-to-left:
```csharp
var dialog = new Dialog(title, width, height, cancelButton, primaryButton);
```

This displays as: `[Cancel] [Primary]`

### Event Handlers

Define event handler methods as local functions:
```csharp
void PrimaryButtonClicked()
{
    // Validation logic
    if (!ValidateInput())
    {
        MessageBox.ErrorQuery(40, 10, "Error", "Invalid input!", "OK");
        return;
    }

    // Collect data
    result.WasCancelled = false;
    Application.RequestStop();
}

void CancelButtonClicked()
{
    result.WasCancelled = true;
    Application.RequestStop();
}
```

---

## Validation and Error Handling

### Validation Pattern

Perform validation in the primary button click handler before closing dialog:

```csharp
void PrimaryButtonClicked()
{
    // Validate each input
    if (!byte.TryParse(addressField.Text.ToString(), out var address))
    {
        MessageBox.ErrorQuery(40, 10, "Error", "Invalid address entered!", "OK");
        return;
    }

    // Additional validations...

    // All validation passed
    result.SomeValue = address;
    result.WasCancelled = false;
    Application.RequestStop();
}
```

### MessageBox Usage

**Error Messages:**
```csharp
MessageBox.ErrorQuery(width, height, title, message, buttonLabel);
```

**Standard Error Format:**
```csharp
MessageBox.ErrorQuery(40, 10, "Error", "Invalid input!", "OK");
```

**Confirmation Dialogs:**
```csharp
var response = MessageBox.Query(width, height, title, message, defaultButton, ...buttons);
```

**Example:**
```csharp
if (MessageBox.Query(60, 10, "Overwrite",
    "Device already exists at that address, overwrite?",
    1, "No", "Yes") == 0)
{
    return; // User selected "No"
}
```

**Parameters:**
- `defaultButton`: 0-based index (0 = first button, 1 = second button)
- Return value: Index of selected button (0, 1, 2, etc.)

### Common Validations

**Numeric Input:**
```csharp
if (!int.TryParse(textField.Text.ToString(), out var value))
{
    MessageBox.ErrorQuery(40, 10, "Error", "Invalid number entered!", "OK");
    return;
}
```

**Byte Range:**
```csharp
if (!byte.TryParse(textField.Text.ToString(), out var value) || value > 127)
{
    MessageBox.ErrorQuery(40, 10, "Error", "Invalid value entered!", "OK");
    return;
}
```

**Empty String:**
```csharp
if (string.IsNullOrEmpty(textField.Text.ToString()))
{
    MessageBox.ErrorQuery(40, 10, "Error", "No value entered!", "OK");
    return;
}
```

**Hex String:**
```csharp
try
{
    var bytes = Convert.FromHexString(textField.Text.ToString()!);
}
catch
{
    MessageBox.ErrorQuery(40, 10, "Error", "Invalid hex characters!", "OK");
    return;
}
```

---

## Focus Management

### Initial Focus

Set focus to the first interactive control (typically first TextField or ComboBox):

```csharp
var nameField = new TextField(15, 1, 35, string.Empty);
var dialog = new Dialog(title, 60, 10, cancelButton, primaryButton);
dialog.Add(new Label(1, 1, "Name:"), nameField);
nameField.SetFocus();

Application.Run(dialog);
```

### Focus for Selection Dialogs

For dialogs with RadioGroup/ListView, set focus to the action button:

```csharp
var sendButton = new Button("Send", true);
var dialog = new Dialog(title, 60, 13, cancelButton, sendButton);
dialog.Add(scrollView);
sendButton.SetFocus();

Application.Run(dialog);
```

---

## Multi-Step Dialogs

Use multi-step dialogs when collecting complex related data.

### Pattern

1. First dialog collects primary parameters with "Next" button
2. After validation, show second dialog
3. If second dialog completes, collect all data

**Example:**
```csharp
public static OutputControlInput Show(DeviceSetting[] devices, string[] deviceList)
{
    var result = new OutputControlInput { WasCancelled = true };

    var outputNumberField = new TextField(25, 1, 25, "0");
    var activateCheckBox = new CheckBox(1, 3, "Activate Output", false);

    void NextButtonClicked()
    {
        // Validate first dialog
        if (!byte.TryParse(outputNumberField.Text.ToString(), out var outputNumber))
        {
            MessageBox.ErrorQuery(40, 10, "Error", "Invalid output number entered!", "OK");
            return;
        }

        Application.RequestStop();

        // Show second dialog
        var deviceSelection = DeviceSelectionDialog.Show("Output Control", devices, deviceList);

        if (!deviceSelection.WasCancelled)
        {
            // Collect all data
            result.OutputNumber = outputNumber;
            result.ActivateOutput = activateCheckBox.Checked;
            result.DeviceAddress = deviceSelection.SelectedDeviceAddress;
            result.WasCancelled = false;
        }
    }

    var nextButton = new Button("Next", true);
    nextButton.Clicked += NextButtonClicked;
    var cancelButton = new Button("Cancel");
    cancelButton.Clicked += CancelButtonClicked;

    var dialog = new Dialog("Output Control", 60, 10, cancelButton, nextButton);
    dialog.Add(new Label(1, 1, "Output Number:"), outputNumberField,
              activateCheckBox);
    outputNumberField.SetFocus();

    Application.Run(dialog);

    return result;
}
```

---

## Result Pattern

### Input Model

All dialogs return an Input model with `WasCancelled` property:

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
public static SomeDialogInput Show(...)
{
    var result = new SomeDialogInput { WasCancelled = true };

    void PrimaryButtonClicked()
    {
        // Validation...

        // Collect data
        result.SomeValue = someField.Text.ToString();
        result.AnotherValue = someValue;
        result.WasCancelled = false;

        Application.RequestStop();
    }

    void CancelButtonClicked()
    {
        result.WasCancelled = true;
        Application.RequestStop();
    }

    // Create and run dialog...

    return result;
}
```

### Caller Pattern

```csharp
var input = SomeDialog.Show(...);
if (!input.WasCancelled)
{
    // Use input.SomeValue, input.AnotherValue
}
```

---

## Code Organization

### File Structure

**Dialogs:**
- Location: `{Console}/Dialogs/`
- Naming: `{Purpose}Dialog.cs` (e.g., `SerialConnectionDialog.cs`)
- One dialog per file

**Input Models:**
- Location: `{Console}/Model/DialogInputs/`
- Naming: `{Purpose}Input.cs` (e.g., `SerialConnectionInput.cs`)
- One model per file

### Dialog Class Structure

```csharp
using System;
using Terminal.Gui;
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
        /// <param name="param1">Description</param>
        /// <returns>{Purpose}Input with user's choices</returns>
        public static {Purpose}Input Show(...)
        {
            var result = new {Purpose}Input { WasCancelled = true };

            // Control definitions

            // Event handlers as local functions
            void PrimaryButtonClicked() { ... }
            void CancelButtonClicked() { ... }

            // Button definitions

            // Dialog creation and layout

            Application.Run(dialog);

            return result;
        }

        // Private helper methods (e.g., CreateComboBox)
    }
}
```

### Consistent Naming

**Variables:**
- Controls: `{purpose}TextField`, `{purpose}ComboBox`, `{purpose}CheckBox`
- Buttons: `{action}Button` (e.g., `startButton`, `cancelButton`)
- Event handlers: `{Action}ButtonClicked` (e.g., `StartButtonClicked`)

**Example:**
```csharp
var portNameComboBox = CreatePortNameComboBox(20, 1, currentSettings.PortName);
var baudRateComboBox = CreateBaudRateComboBox(20, 3, currentSettings.BaudRate);

void StartConnectionButtonClicked() { ... }
void CancelButtonClicked() { ... }

var startButton = new Button("Start", true);
startButton.Clicked += StartConnectionButtonClicked;
```

---

## Examples

### Simple Dialog (Single Step)

```csharp
using System;
using System.IO.Ports;
using Terminal.Gui;
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
        /// <param name="currentSettings">Current connection settings for defaults</param>
        /// <returns>SerialConnectionInput with user's choices</returns>
        public static SerialConnectionInput Show(ConnectionSettings currentSettings)
        {
            var result = new SerialConnectionInput { WasCancelled = true };

            var portNameComboBox = CreatePortNameComboBox(15, 1, currentSettings.SerialPortName)
                .ConfigureForOptimalUX();
            var baudRateComboBox = CreateBaudRateComboBox(15, 3, currentSettings.SerialBaudRate)
                .ConfigureForOptimalUX();

            void StartButtonClicked()
            {
                // Validate port name
                if (string.IsNullOrEmpty(portNameComboBox.Text.ToString()))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "No port name selected!", "OK");
                    return;
                }

                // Validate baud rate
                if (!int.TryParse(baudRateComboBox.Text.ToString(), out var baudRate))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid baud rate selected!", "OK");
                    return;
                }

                // All validation passed - collect the data
                result.PortName = portNameComboBox.Text.ToString();
                result.BaudRate = baudRate;
                result.WasCancelled = false;

                Application.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                Application.RequestStop();
            }

            var startButton = new Button("Start", true);
            startButton.Clicked += StartButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelButtonClicked;

            var dialog = new Dialog("Serial Connection Settings", 60, 10, cancelButton, startButton);
            dialog.Add(new Label(1, 1, "Port:"), portNameComboBox,
                      new Label(1, 3, "Baud Rate:"), baudRateComboBox);
            portNameComboBox.SetFocus();

            Application.Run(dialog);

            return result;
        }

        private static ComboBox CreatePortNameComboBox(int x, int y, string currentPortName)
        {
            var portNames = SerialPort.GetPortNames();

            if (portNames.Length == 0)
            {
                portNames = ["No ports available"];
            }

            // IMPORTANT: Width must be at least ComboBoxExtensions.MinimumRecommendedWidth (30)
            var portNameComboBox = new ComboBox(new Rect(x, y, 30, 5), portNames);

            if (portNames.Length > 0 && !portNames[0].Equals("No ports available"))
            {
                var index = Array.FindIndex(portNames, port =>
                    string.Equals(port, currentPortName, StringComparison.OrdinalIgnoreCase));
                portNameComboBox.SelectedItem = Math.Max(index, 0);
            }

            return portNameComboBox;
        }

        private static ComboBox CreateBaudRateComboBox(int x, int y, int currentBaudRate)
        {
            // IMPORTANT: Width must be at least ComboBoxExtensions.MinimumRecommendedWidth (30)
            var baudRateComboBox = new ComboBox(new Rect(x, y, 30, 5), StandardBaudRates);

            var currentBaudRateString = currentBaudRate.ToString();
            var index = Array.FindIndex(StandardBaudRates, rate =>
                string.Equals(rate, currentBaudRateString));
            baudRateComboBox.SelectedItem = Math.Max(index, 0);

            return baudRateComboBox;
        }
    }
}
```

### Complex Dialog (Multiple Controls)

```csharp
using System;
using System.Linq;
using Terminal.Gui;
using ACUConsole.Configuration;
using ACUConsole.Model.DialogInputs;

namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Dialog for collecting device addition parameters
    /// </summary>
    public static class AddDeviceDialog
    {
        /// <summary>
        /// Shows the add device dialog and returns user input
        /// </summary>
        /// <param name="existingDevices">List of existing devices to check for duplicates</param>
        /// <returns>AddDeviceInput with user's choices</returns>
        public static AddDeviceInput Show(DeviceSetting[] existingDevices)
        {
            var result = new AddDeviceInput { WasCancelled = true };

            var nameTextField = new TextField(15, 1, 35, string.Empty);
            var addressTextField = new TextField(15, 3, 35, string.Empty);
            var useCrcCheckBox = new CheckBox(1, 5, "Use CRC", true);
            var useSecureChannelCheckBox = new CheckBox(1, 6, "Use Secure Channel", true);
            var keyTextField = new TextField(15, 8, 35, Convert.ToHexString(DeviceSetting.DefaultKey));

            void AddDeviceButtonClicked()
            {
                // Validate address
                if (!byte.TryParse(addressTextField.Text.ToString(), out var address) || address > 127)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid address entered!", "OK");
                    return;
                }

                // Validate key length
                if (keyTextField.Text == null || keyTextField.Text.Length != 32)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid key length entered!", "OK");
                    return;
                }

                // Validate hex key format
                byte[] key;
                try
                {
                    key = Convert.FromHexString(keyTextField.Text.ToString()!);
                }
                catch
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid hex characters!", "OK");
                    return;
                }

                // Check for existing device at address
                var existingDevice = existingDevices.FirstOrDefault(d => d.Address == address);
                bool overwriteExisting = false;
                if (existingDevice != null)
                {
                    if (MessageBox.Query(60, 10, "Overwrite",
                        "Device already exists at that address, overwrite?",
                        1, "No", "Yes") == 0)
                    {
                        return;
                    }
                    overwriteExisting = true;
                }

                // All validation passed - collect the data
                result.Name = nameTextField.Text.ToString();
                result.Address = address;
                result.UseCrc = useCrcCheckBox.Checked;
                result.UseSecureChannel = useSecureChannelCheckBox.Checked;
                result.SecureChannelKey = key;
                result.OverwriteExisting = overwriteExisting;
                result.WasCancelled = false;

                Application.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                Application.RequestStop();
            }

            var addButton = new Button("Add", true);
            addButton.Clicked += AddDeviceButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelButtonClicked;

            var dialog = new Dialog("Add Device", 60, 13, cancelButton, addButton);
            dialog.Add(new Label(1, 1, "Name:"), nameTextField,
                      new Label(1, 3, "Address:"), addressTextField,
                      useCrcCheckBox,
                      useSecureChannelCheckBox,
                      new Label(1, 8, "Secure Key:"), keyTextField);
            nameTextField.SetFocus();

            Application.Run(dialog);

            return result;
        }
    }
}
```

### Selection Dialog with ScrollView

```csharp
using System.Linq;
using NStack;
using Terminal.Gui;
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
        /// <param name="title">Dialog title</param>
        /// <param name="devices">Available devices to choose from</param>
        /// <param name="deviceList">Formatted device list for display</param>
        /// <returns>DeviceSelectionInput with user's choice</returns>
        public static DeviceSelectionInput Show(string title, DeviceSetting[] devices, string[] deviceList)
        {
            var result = new DeviceSelectionInput { WasCancelled = true };

            var scrollView = new ScrollView(new Rect(6, 1, 50, 6))
            {
                ContentSize = new Size(40, deviceList.Length * 2),
                ShowVerticalScrollIndicator = deviceList.Length > 6,
                ShowHorizontalScrollIndicator = false
            };

            var deviceRadioGroup = new RadioGroup(0, 0, deviceList.Select(ustring.Make).ToArray())
            {
                SelectedItem = 0
            };
            scrollView.Add(deviceRadioGroup);

            void SendCommandButtonClicked()
            {
                var selectedDevice = devices.OrderBy(device => device.Address).ToArray()[deviceRadioGroup.SelectedItem];
                result.SelectedDeviceAddress = selectedDevice.Address;
                result.WasCancelled = false;
                Application.RequestStop();
            }

            void CancelButtonClicked()
            {
                result.WasCancelled = true;
                Application.RequestStop();
            }

            var sendButton = new Button("Send", true);
            sendButton.Clicked += SendCommandButtonClicked;
            var cancelButton = new Button("Cancel");
            cancelButton.Clicked += CancelButtonClicked;

            var dialog = new Dialog(title, 60, 13, cancelButton, sendButton);
            dialog.Add(scrollView);
            sendButton.SetFocus();

            Application.Run(dialog);

            return result;
        }
    }
}
```

---

## Summary Checklist

When creating a new dialog, ensure:

- [ ] Dialog width is 60 characters (standard)
- [ ] Dialog height accommodates all controls with proper spacing
- [ ] Labels positioned at x=1 with colon
- [ ] Controls aligned consistently (x=15, 20, or 25)
- [ ] Vertical spacing uses y increment of 2 (or 1 for related items)
- [ ] ComboBox width is minimum 30 characters
- [ ] ComboBox uses `.ConfigureForOptimalUX()` extension
- [ ] TextField widths are 25, 30, or 35
- [ ] Primary button is marked as default (second parameter true)
- [ ] Buttons ordered as: cancelButton, primaryButton
- [ ] Event handlers are local functions
- [ ] Validation occurs before closing dialog
- [ ] Error messages use MessageBox.ErrorQuery
- [ ] Result pattern uses WasCancelled property
- [ ] First control receives focus
- [ ] Static class with static Show() method
- [ ] XML documentation on class and method
- [ ] File in correct location (Dialogs/ or Model/DialogInputs/)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-10-27 | Initial version based on PDConsole and ACUConsole patterns |
