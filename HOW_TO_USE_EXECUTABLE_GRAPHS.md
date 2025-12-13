# How to Use Executable Graph Files - Step by Step Guide

## Quick Start

### Step 1: Create a Graph
1. **Open the Graph Simulator application**
2. **Add a Start Node:**
   - Click "Add Node" button or right-click on canvas
   - Select **"start"** from the node type dropdown
   - This is your execution entry point (required!)

3. **Add Operation Nodes:**
   - Add more nodes (mouse_click, type_text, wait, etc.)
   - Configure each node's properties in the sidebar
   - Link nodes together by dragging from one node to another

4. **Connect the Nodes:**
   - Click and drag from the **start** node to your first operation
   - Continue linking nodes in the order you want them to execute
   - Each node can have 1 incoming and 1 outgoing link

**Example workflow:**
```
[Start] → [Mouse Click] → [Type Text] → [Wait] → [Key Press]
```

### Step 2: Save as Executable
1. Click **File** menu → **Save as Executable**
2. Choose where to save (e.g., your Desktop)
3. Enter a name: `MyAutomation.json`
4. Click **Save**

**Result:** Two files are created:
- ✅ `MyAutomation.json` - Your graph file
- ✅ `MyAutomation.bat` - The executable launcher

### Step 3: Run Your Graph
**Method 1: Double-Click** (Easiest)
- Just **double-click** `MyAutomation.bat`
- The application will launch and execute automatically

**Method 2: From Desktop** (Quick Access)
1. Right-click `MyAutomation.bat` → Send to → Desktop (create shortcut)
2. Now you can run it directly from your desktop!

**Method 3: Command Line**
```bash
# Windows Command Prompt or PowerShell
cd "C:\Path\To\Your\Files"
MyAutomation.bat
```

### Step 4: Edit Your Graph (Anytime)
1. **Open the application**
2. Click **File → Open**
3. Select `MyAutomation.json`
4. Make your changes
5. Click **Save** (Ctrl+S)
6. Your `.bat` file automatically uses the updated graph!

## Real-World Examples

### Example 1: Auto-Login Script
**What it does:** Opens a website and logs in automatically

**Nodes:**
```
[Start] 
  ↓
[Mouse Click] (Click address bar at position X:100, Y:50)
  ↓
[Type Text] "https://example.com"
  ↓
[Key Press] (Enter key)
  ↓
[Wait] (3000ms - wait for page to load)
  ↓
[Mouse Click] (Click username field at X:500, Y:300)
  ↓
[Type Text] "myusername"
  ↓
[Mouse Click] (Click password field at X:500, Y:350)
  ↓
[Type Text] "mypassword"
  ↓
[Mouse Click] (Click login button at X:500, Y:400)
```

**Save as:** `AutoLogin.json` → Creates `AutoLogin.bat`
**Run:** Double-click `AutoLogin.bat` every morning!

### Example 2: Copy-Paste Automation
**What it does:** Copies text from one application and pastes to another

**Nodes:**
```
[Start]
  ↓
[Mouse Click] (Click first application at X:200, Y:300)
  ↓
[Key Press] (Ctrl+A - Select all)
  ↓
[Key Press] (Ctrl+C - Copy)
  ↓
[Wait] (500ms)
  ↓
[Mouse Click] (Click second application at X:800, Y:300)
  ↓
[Key Press] (Ctrl+V - Paste)
```

**Save as:** `CopyBetweenApps.json` → Creates `CopyBetweenApps.bat`

### Example 3: Nested Graphs (Advanced)
**What it does:** Reuses common workflows

**Main Graph:**
```
[Start]
  ↓
[Graph] (Points to "OpenApp.json")
  ↓
[Type Text] "Hello World"
  ↓
[Graph] (Points to "SaveAndClose.json")
```

**Reusable Sub-Graphs:**
- `OpenApp.json` - Opens your application
- `SaveAndClose.json` - Saves and closes the app

**Benefits:** Update `OpenApp.json` once, all graphs using it get the update!

## Advanced Usage

### Schedule Automatic Execution (Windows Task Scheduler)
1. Open **Task Scheduler** (search in Start menu)
2. Click **"Create Basic Task"**
3. Name it: "Daily Automation"
4. Choose trigger: "Daily" at 9:00 AM
5. Action: **"Start a program"**
6. Browse to your `.bat` file: `C:\Path\To\MyAutomation.bat`
7. Click **Finish**

**Result:** Your graph runs automatically every day at 9 AM!

### Create Desktop Shortcuts
1. Right-click your `.bat` file
2. Select **"Create shortcut"**
3. Drag the shortcut to your Desktop
4. (Optional) Right-click shortcut → Properties → Change Icon
5. Now you have a one-click automation button!

### Run Multiple Graphs in Sequence
Create a master batch file (`RunAll.bat`):
```batch
@echo off
echo Running automation sequence...

call "Step1-Login.bat"
timeout /t 5
call "Step2-ProcessData.bat"
timeout /t 5
call "Step3-Logout.bat"

echo All automations complete!
pause
```

### Command-Line Arguments
```bash
# Run without showing the dialog
GraphSimulator.exe "MyGraph.json" --execute

# Just load the graph (for editing)
GraphSimulator.exe "MyGraph.json"

# Run from PowerShell
& "C:\Program Files\GraphSimulator\GraphSimulator.exe" "C:\Automations\MyGraph.json" --execute
```

## Tips & Best Practices

### ✅ DO:
- **Always start with a "start" node** - Required for execution
- **Test your graph** before saving as executable (use the Execute button in app)
- **Add Wait nodes** between operations to ensure proper timing
- **Use descriptive names** for your graph files
- **Keep .json and .bat files together** in the same folder
- **Use mouse coordinates** carefully - position windows consistently
- **Save graphs often** while building them (Ctrl+S)

### ❌ DON'T:
- **Don't separate** the `.json` and `.bat` files (they work as a pair)
- **Don't edit** the `.bat` file unless you know what you're doing
- **Don't use hardcoded delays** - use Wait nodes instead
- **Don't delete** the `.json` file if you want to edit later
- **Don't run multiple instances** of the same graph simultaneously

## Troubleshooting

### Problem: Double-clicking .bat does nothing
**Solution:** 
- Right-click the `.bat` file → Edit
- Verify the path to `GraphSimulator.exe` is correct
- Update the path if you moved the application

### Problem: Graph doesn't execute automatically
**Solution:**
- Open the `.json` file in the application
- Verify you have a **start** node
- Check that all nodes are linked correctly
- Test execution with the "Execute" button in the app

### Problem: Mouse clicks are in wrong positions
**Solution:**
- Open the graph and edit the mouse click coordinates
- Make sure your screen resolution matches when you recorded the positions
- Consider using Window-relative positions instead of absolute screen positions

### Problem: Operations happen too fast
**Solution:**
- Add **Wait** nodes between operations
- Typical wait times:
  - After mouse click: 100-500ms
  - After typing: 500-1000ms
  - After opening apps: 2000-5000ms
  - After loading web pages: 3000-10000ms

## Getting Help

### Check the Graph Visually
1. Open your `.json` file in the application
2. Look at the canvas - all nodes should be connected
3. The execution path should be clear: Start → Node1 → Node2 → etc.

### Test Before Deploying
1. Open the graph in the app
2. Click **Execute** button (or press the execute shortcut)
3. Watch it run and verify each step works
4. Only after successful test, save as executable

### Enable Frequency (Advanced)
Each operation node has a **Frequency** property:
- Set frequency to repeat an operation multiple times
- Example: Click a button 5 times → Set Frequency = 5
- Useful for: Pagination, multiple data entries, etc.

## Summary: Complete Workflow

```
CREATE:
1. Build graph in application (Start node + Operations)
2. Link nodes in execution order
3. Test with Execute button
4. File → Save as Executable

RUN:
1. Double-click the .bat file
   OR
2. Run from command line
   OR  
3. Schedule with Task Scheduler

EDIT:
1. Open the .json file in application
2. Make changes
3. Save (Ctrl+S)
4. The .bat file automatically uses new version

SHARE:
1. Copy both .json and .bat files
2. Send to colleagues
3. They can run the .bat file immediately
```

## Next Steps

Now that you know the basics:
1. **Create your first automation** - Start simple (like opening an app)
2. **Test thoroughly** - Run it multiple times to ensure reliability
3. **Build a library** - Create reusable sub-graphs
4. **Automate daily tasks** - Save time on repetitive work!

Happy Automating! 🚀
