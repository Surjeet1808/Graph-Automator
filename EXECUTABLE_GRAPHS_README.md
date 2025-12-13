# Executable Graph Files

## Overview
The Graph Simulator now supports saving graphs as executable files that can be run directly without opening the application manually.

## How to Create an Executable Graph

### Method 1: Using the Application Menu
1. Create or open a graph in the application
2. Go to **File → Save as Executable**
3. Choose a location and filename (e.g., `MyAutomation.json`)
4. Click Save

### What Gets Created
When you save as executable, two files are created:
- **`MyAutomation.json`** - The graph data file (editable)
- **`MyAutomation.bat`** - The launcher batch file (executable)

## How to Use

### Running the Graph
- **Double-click** the `.bat` file (e.g., `MyAutomation.bat`)
- The application will automatically:
  1. Launch
  2. Load the graph
  3. Execute it immediately

### Editing the Graph
- **Open** the `.json` file through the application:
  - Method 1: Launch the application and use **File → Open**
  - Method 2: Right-click the `.json` file → Open With → GraphSimulator
- Make your changes
- Use **File → Save** to update the graph
- The executable `.bat` file will automatically run the updated version

## Command-Line Usage

You can also run graphs from the command line:

```bash
# Execute a graph automatically
GraphSimulator.exe "path/to/graph.json" --execute

# Open a graph for editing (no auto-execute)
GraphSimulator.exe "path/to/graph.json"
```

## Technical Details

### How It Works
1. The `.bat` file contains a command that launches the application with:
   - The graph file path as the first argument
   - The `--execute` flag to trigger automatic execution

2. On startup, the application checks for command-line arguments:
   - If a file path is provided, it loads that graph
   - If `--execute` flag is present, it immediately runs the graph

### File Association (Optional)
You can associate `.json` graph files with the GraphSimulator application:
1. Right-click any `.json` graph file
2. Select "Open With" → "Choose another app"
3. Browse to `GraphSimulator.exe`
4. Check "Always use this app"

## Benefits

✅ **Quick Execution** - Run automation workflows with a double-click
✅ **Editable** - Graphs remain fully editable when opened through the application
✅ **Portable** - Both files can be moved together to any location
✅ **Version Control** - The `.json` file can be tracked in Git
✅ **Shareable** - Send both files to others to share automation workflows

## Example Use Cases

1. **Daily Task Automation** - Create a `.bat` file on your desktop to run daily tasks
2. **Testing Scripts** - Quickly execute test sequences without navigating menus
3. **Batch Processing** - Chain multiple graphs together in a master `.bat` file
4. **Scheduled Tasks** - Use Windows Task Scheduler to run `.bat` files automatically

## Notes

- Keep the `.json` and `.bat` files together in the same directory
- The `.bat` file uses relative paths, so both files can be moved together
- Edit graphs by opening the `.json` file in the application
- The `.bat` file doesn't need to be regenerated after editing the graph
