# Dynamic Values UI Quick Reference

## Value Mode Selection

When you select a node, you'll see:

```
┌─────────────────────────────────────────┐
│  Value Mode                              │
├─────────────────────────────────────────┤
│  ○ Static Values                         │
│    Enter fixed values manually           │
│                                          │
│  ● Dynamic Values                        │
│    Get values from data source           │
└─────────────────────────────────────────┘
```

## Dynamic Source Type Selection

When "Dynamic Values" is selected:

```
┌─────────────────────────────────────────────────┐
│  Select Dynamic Source Type:                    │
├─────────────────────────────────────────────────┤
│  📅 Date-Based Array                     [▼]   │
│     Values change daily based on start date     │
│                                                 │
│  ✅ Date-Based Array is fully functional       │
│  🚧 Other types are in development              │
└─────────────────────────────────────────────────┘
```

## Available Options in Dropdown

### ✅ Functional
```
📅 Date-Based Array
   Values change daily based on start date
```

### 🚧 In Development (Disabled)
```
🔄 Iteration-Based Array (In Development)
   Cycle through array with each execution

🌐 API Source (In Development)
   Fetch values from REST API endpoints

📊 Excel/CSV File (In Development)
   Read values from spreadsheet files

⚙️ Condition-Based Calculation (In Development)
   Calculate values based on custom conditions

📆 Date Expression (In Development)
   Calculate using date/time formulas

💻 Custom Expression (In Development)
   Use C# expressions for complex logic
```

## When Date-Based Array is Selected

```
┌─────────────────────────────────────────────────┐
│  📅 Date-Based Array Configuration              │
│  Values are selected based on days elapsed      │
│  since start date.                              │
├─────────────────────────────────────────────────┤
│  Start Date:                                    │
│  [📅 Date Picker]                               │
│  First array item will be used on this date     │
│                                                 │
│  Data Array (JSON Array):                       │
│  ┌─────────────────────────────────────────┐  │
│  │ ["{\"x\":100,\"y\":200}",               │  │
│  │  "{\"x\":150,\"y\":250}",               │  │
│  │  "{\"x\":200,\"y\":300}"]               │  │
│  └─────────────────────────────────────────┘  │
│  Example for mouse click:                       │
│  ["{\"x\":100,\"y\":200}", ...]                 │
│  Each item = one day.                           │
│                                                 │
│  Value Mappings (JSON):                         │
│  ┌─────────────────────────────────────────┐  │
│  │ {"IntValues[0]": "$.x",                 │  │
│  │  "IntValues[1]": "$.y"}                 │  │
│  └─────────────────────────────────────────┘  │
│  Map JSON fields to operation values:           │
│  {"IntValues[0]": "$.x", "IntValues[1]": "$.y"} │
└─────────────────────────────────────────────────┘
```

## When In-Development Type is Selected

```
┌─────────────────────────────────────────────────┐
│  🚧 IN DEVELOPMENT                              │
├─────────────────────────────────────────────────┤
│  This dynamic source type is currently          │
│  being developed.                               │
│                                                 │
│  [Specific description of the feature]          │
│                                                 │
│  ✅ Date-Based Array is fully functional        │
│     and ready to use!                           │
└─────────────────────────────────────────────────┘
```

## Complete Workflow

```
1. Create/Select Node
   │
2. Select "Dynamic Values" radio button
   │
3. Dynamic Source Panel appears
   │
4. Select Source Type from dropdown
   │
   ├─ Date-Based Array (functional)
   │  │
   │  ├─ Set Start Date
   │  ├─ Enter Data Array JSON
   │  └─ Configure Value Mappings
   │
   └─ Other types (in development)
      │
      └─ Show "IN DEVELOPMENT" message
   
5. Save Node Properties
   │
6. Execute Graph
   │
   └─ Values are resolved at runtime
```

## Visual Indicators

| Color | Meaning |
|-------|---------|
| 🟦 Blue Border | Value Mode selection panel |
| 🟧 Orange Border | Dynamic source configuration |
| 🟨 Yellow Border | "In Development" placeholder |
| 🟩 Green Text | Functional/Ready to use |
| ⚫ Gray Text | Disabled/In development |

## Tips

- **Green checkmark (✅)** = Feature is ready to use
- **Construction sign (🚧)** = Feature is in development
- **Gray text** = Option is currently disabled
- **Emoji icons** = Quick visual identification of source types
