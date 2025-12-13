# UX Enhancements Guide

## New Features Added

### 1. 📋 Special Key Codes Reference (Expandable Hint)

**Location:** Properties Panel → Key Operations section

**What it does:**
- Shows a comprehensive list of common key codes for key_press, key_down, and key_up operations
- Expandable/collapsible panel to save space
- No more guessing key codes!

**Key Codes Include:**
- **Special Keys:** Backspace (8), Tab (9), Enter (13), Escape (27), Space (32), Delete (46)
- **Function Keys:** F1-F12 (112-123)
- **Arrow Keys:** Left (37), Up (38), Right (39), Down (40)
- **Modifiers:** Shift (16), Ctrl (17), Alt (18)
- **Letters:** A-Z (65-90)
- **Numbers:** 0-9 (48-57), Numpad 0-9 (96-105)

**How to use:**
1. Select a node with Key Press/Down/Up operation
2. Look for "Common Key Codes (Click to view)" 
3. Click to expand and see all available key codes
4. Copy the number you need into the Key Code field

---

### 2. ↔️ Horizontal Scroll Support

**New Node Types:** `scroll_left` and `scroll_right`

**What it does:**
- Enables horizontal scrolling in your automations
- Works with trackpad two-finger horizontal swipe
- Useful for wide spreadsheets, timelines, and horizontal content

**How to use:**
1. Add a node and select type `scroll_left` or `scroll_right`
2. Set the scroll amount (default: 120)
   - Positive numbers = more scroll
   - Negative numbers = less scroll
3. Link it in your automation sequence

**Example use cases:**
- Scrolling through wide Excel sheets
- Navigating horizontal timelines
- Moving through carousel content
- Swiping in web applications

**Colors:**
- `scroll_left` = Dark olive green (#3A5A1F)
- `scroll_right` = Dark moss green (#254010)

---

### 3. 📏 Scroll Amount Measurement Tool

**Location:** Top toolbar (next to Mouse Position tracker)

**What it does:**
- **Real-time scroll measurement** while creating automations
- Shows exactly how much you scrolled with trackpad/mouse
- Displays positive (↑) and negative (↓) scroll values
- Includes Reset button to start fresh

**How to use:**

**Step 1: Enable Measurement**
1. Check the **"Scroll Measure"** checkbox in the toolbar
2. The scroll counter appears showing "Scroll: 0"

**Step 2: Test Your Scroll**
1. Use your trackpad (two-finger scroll) or mouse wheel
2. **Scroll up** = Positive numbers (e.g., +240, +480)
3. **Scroll down** = Negative numbers (e.g., -120, -360)
4. The counter updates in real-time

**Step 3: Note the Value**
1. Watch the counter as you scroll
2. When you reach the desired amount, note the number
3. Example: "Scroll: 480" means you scrolled 480 units

**Step 4: Use in Your Automation**
1. Add a `scroll_up`, `scroll_down`, `scroll_left`, or `scroll_right` node
2. Enter the measured value in "Scroll Amount" field
3. Your automation will replicate that exact scroll!

**Step 5: Reset and Test Again**
1. Click **"Reset"** button to set counter back to 0
2. Test different scroll amounts
3. Build precise scroll sequences

**Example Workflow:**
```
Task: Scroll down a webpage to reach a button

1. Enable "Scroll Measure"
2. Open the target webpage
3. Manually scroll down to the button position
4. Note the value: "Scroll: -720"
5. Reset counter
6. Add scroll_down node with amount: 720
7. Your automation will scroll exactly to that position!
```

**Tips:**
- ✅ **Vertical scroll:** Use scroll_up (positive) / scroll_down (negative)
- ✅ **Horizontal scroll:** Use scroll_left (negative) / scroll_right (positive)
- ✅ **Two-finger swipe:** Works with both vertical and horizontal
- ✅ **Mouse wheel:** Typically gives ±120 per click
- ✅ **Trackpad:** Smoother, variable amounts
- ✅ **Precision:** Test multiple times and average the values

**Display Format:**
```
Scroll: 480 (↑positive / ↓negative)
```
- **480** = The accumulated scroll amount
- **↑positive** = Scrolling up (or right for horizontal)
- **↓negative** = Scrolling down (or left for horizontal)

---

## Benefits of These Enhancements

### 🎯 Increased Precision
- Know exact key codes without external references
- Measure precise scroll amounts for reliable automations
- No more guessing or trial-and-error

### ⚡ Faster Workflow
- Expandable key code reference saves time
- Real-time scroll measurement speeds up development
- Reset button allows quick iteration

### 📱 Better UX
- Clean, organized interface
- Contextual help when needed
- Visual feedback for all actions

### 🔄 More Automation Options
- Horizontal scrolling opens new possibilities
- Precise scroll control for complex UIs
- Support for trackpad gestures

---

## Quick Reference Card

| Feature | Location | Shortcut |
|---------|----------|----------|
| Key Codes | Properties Panel → Key Operations | Expand panel |
| Scroll Left | Node Type Dropdown | - |
| Scroll Right | Node Type Dropdown | - |
| Scroll Measure | Top Toolbar | Check/Uncheck |
| Reset Counter | Next to Scroll Display | Click Reset |

---

## Examples

### Example 1: Precise Page Scroll
**Goal:** Scroll down exactly 3 "screens" worth

```
1. Enable Scroll Measure
2. Manually scroll down 3 screens
3. Note value: -960
4. Reset
5. Add scroll_down node with amount: 960
```

### Example 2: Horizontal Gallery Navigation
**Goal:** Swipe through 5 images in a horizontal gallery

```
1. Enable Scroll Measure
2. Two-finger swipe left to reach image 5
3. Note value: -600
4. Reset
5. Add scroll_left node with amount: 600
```

### Example 3: Complex Key Sequence
**Goal:** Press Ctrl+Shift+P (VS Code command palette)

```
Using Key Code Reference:
1. Add key_down node: 17 (Ctrl)
2. Add key_down node: 16 (Shift)
3. Add key_press node: 80 (P)
4. Add key_up node: 16 (Shift)
5. Add key_up node: 17 (Ctrl)
```

---

## Troubleshooting

**Q: Scroll measurement not working?**
- A: Make sure the checkbox is enabled and the window has focus

**Q: Scroll amount differs from actual automation?**
- A: Different apps may interpret scroll values differently. Test and adjust.

**Q: Key codes hint not showing?**
- A: Select a node with key_press/key_down/key_up operation type first

**Q: Horizontal scroll not working?**
- A: Not all applications support horizontal mouse wheel events

---

## Technical Notes

### Scroll Measurement
- Uses `PreviewMouseWheel` event
- Tracks `MouseWheelEventArgs.Delta`
- Positive delta = scroll up/right
- Negative delta = scroll down/left
- Standard mouse wheel click = ±120 units
- Trackpad gives variable amounts based on gesture speed

### Horizontal Scroll
- Uses Windows `MOUSEEVENTF_HWHEEL` event (0x01000)
- Requires Windows Vista or later
- Application must support horizontal wheel messages
- Works best with native Windows applications

### Key Codes
- Based on Virtual-Key Codes from Windows API
- Values are in decimal (not hexadecimal)
- Case-insensitive for letters (A=65, a=65)
- Modifier keys need separate down/up events for combinations

---

Enjoy the enhanced automation experience! 🚀
