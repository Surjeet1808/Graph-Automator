# Dynamic Values Feature - User Guide

## Overview
The Graph Automator now supports **Dynamic Values** where node operation values can be determined at runtime based on various sources. 

### Available Source Types

#### ✅ Fully Functional
- **📅 Date-Based Array** - Values change daily based on start date (ready to use!)

#### 🚧 In Development
- **🔄 Iteration-Based Array** - Cycle through array with each execution
- **🌐 API Source** - Fetch values from REST API endpoints
- **📊 Excel/CSV File** - Read values from spreadsheet files
- **⚙️ Condition-Based Calculation** - Calculate values based on custom conditions
- **📆 Date Expression** - Calculate using date/time formulas
- **💻 Custom Expression** - Use C# expressions for complex logic

*Currently, only Date-Based Array is fully implemented and functional. Other types will show "In Development" placeholder.*

## How It Works

### Value Modes
Each node can operate in one of two modes:

1. **Static Mode** (Default)
   - Values are hardcoded and never change
   - You manually enter coordinates, text, key codes, etc.
   - Example: Always click at position (100, 200)

2. **Dynamic Mode**
   - Values are fetched from a configured data source at runtime
   - Values change automatically based on the source
   - Example: Click at different positions each day

### Date-Based Arrays
When using **Date-Based Array** dynamic source:
- You define a **Start Date** (when the array begins)
- You provide an **array of data items** (JSON objects)
- Each array item represents values for **one day**
- The system calculates days elapsed since start date
- The corresponding array item is used based on the current date

#### Example Scenario
```
Start Date: 2025-01-01
Data Array: [item0, item1, item2, item3, item4]

- On 2025-01-01 → Uses item0 (day 0)
- On 2025-01-02 → Uses item1 (day 1)
- On 2025-01-03 → Uses item2 (day 2)
- On 2025-01-04 → Uses item3 (day 3)
- On 2025-01-05 → Uses item4 (day 4)
- On 2025-01-06 → ERROR! Array exceeded, execution stops
```

### Safety Features
⚠️ **Execution stops with an alert if:**
- Current date is **before** the start date
- Current date exceeds the array length (no more data available)
- This prevents errors and unintended behavior

## Using Dynamic Values

### Step-by-Step Guide

#### 1. Create or Select a Node
- Add a node or click on an existing node to select it

#### 2. Choose Value Mode
In the **Value Mode** section:
- Select **"Dynamic Values"** radio button
- The dynamic source configuration panel will appear
- Static parameter fields will be hidden

#### 3. Configure Dynamic Source

**Select Source Type:** 
Choose from the dropdown (currently only Date-Based Array is functional):
- **📅 Date-Based Array** ✅ Ready to use
- Other types show "🚧 IN DEVELOPMENT" placeholder

**For Date-Based Array:**

**Start Date:** 
- Choose the date when the array should begin
- First array item will be used on this date

**Data Array (JSON Array):**
Enter an array of JSON objects. Each object = values for one day.

**Example for Mouse Click:**
```json
[
  "{\"x\":100,\"y\":200}", 
  "{\"x\":150,\"y\":250}", 
  "{\"x\":200,\"y\":300}",
  "{\"x\":250,\"y\":350}",
  "{\"x\":300,\"y\":400}"
]
```

**Example for Type Text:**
```json
[
  "{\"text\":\"Monday message\"}", 
  "{\"text\":\"Tuesday message\"}", 
  "{\"text\":\"Wednesday message\"}"
]
```

**Value Mappings (JSON):**
Map the JSON fields to operation parameters:

**For Mouse Operations:**
```json
{"IntValues[0]": "$.x", "IntValues[1]": "$.y"}
```

**For Type Text:**
```json
{"StringValues[0]": "$.text"}
```

**For Key Press:**
```json
{"IntValues[0]": "$.keyCode"}
```

**For Scroll:**
```json
{"IntValues[0]": "$.amount"}
```

#### 4. Save Node Properties
- Click the **Save** button to apply changes
- The node's JSON data will include the dynamic configuration

#### 5. Execute the Graph
- Click **▶ Start Execution**
- The system will:
  1. Calculate days since start date
  2. Select the appropriate array item
  3. Extract values using the mappings
  4. Execute the operation with those values

## Complete Examples

### Example 1: Daily Mouse Click Positions

**Operation:** Mouse Left Click  
**Value Mode:** Dynamic  
**Source Type:** DateBasedArray  
**Start Date:** 2025-12-23  

**Data Array:**
```json
[
  "{\"x\":100,\"y\":100}",
  "{\"x\":200,\"y\":200}",
  "{\"x\":300,\"y\":300}",
  "{\"x\":400,\"y\":400}",
  "{\"x\":500,\"y\":500}"
]
```

**Value Mappings:**
```json
{"IntValues[0]": "$.x", "IntValues[1]": "$.y"}
```

**Result:**
- Dec 23, 2025 → Clicks at (100, 100)
- Dec 24, 2025 → Clicks at (200, 200)
- Dec 25, 2025 → Clicks at (300, 300)
- Dec 26, 2025 → Clicks at (400, 400)
- Dec 27, 2025 → Clicks at (500, 500)
- Dec 28, 2025 → Stops with alert: "Array exceeded"

### Example 2: Daily Text Messages

**Operation:** Type Text  
**Value Mode:** Dynamic  
**Source Type:** DateBasedArray  
**Start Date:** 2025-12-01  

**Data Array:**
```json
[
  "{\"text\":\"Week 1 report\"}",
  "{\"text\":\"Week 1 report\"}",
  "{\"text\":\"Week 1 report\"}",
  "{\"text\":\"Week 1 report\"}",
  "{\"text\":\"Week 1 report\"}",
  "{\"text\":\"Week 1 report\"}",
  "{\"text\":\"Week 1 report\"}",
  "{\"text\":\"Week 2 report\"}",
  "{\"text\":\"Week 2 report\"}"
]
```

**Value Mappings:**
```json
{"StringValues[0]": "$.text"}
```

### Example 3: Progressive Scroll Amounts

**Operation:** Scroll Down  
**Value Mode:** Dynamic  
**Source Type:** DateBasedArray  
**Start Date:** 2025-12-20  

**Data Array:**
```json
[
  "{\"amount\":120}",
  "{\"amount\":240}",
  "{\"amount\":360}",
  "{\"amount\":480}",
  "{\"amount\":600}"
]
```

**Value Mappings:**
```json
{"IntValues[0]": "$.amount"}
```

## Error Messages

### "Current date is BEFORE the start date"
**Cause:** You're trying to execute before the start date  
**Solution:** Wait until the start date or update the start date

### "DATE-BASED ARRAY EXCEEDED"
**Cause:** Array has run out of data (current date > start date + array length)  
**Solutions:**
- Add more items to the data array
- Update the start date to a more recent date
- Check if the graph should still be running

### "Invalid JSON at array index X"
**Cause:** One of the array items has malformed JSON  
**Solution:** Fix the JSON syntax at that position

### "Failed to extract value from path"
**Cause:** The JSON path in value mappings doesn't exist in the data  
**Solution:** Check that the JSON fields match the mappings

## Tips & Best Practices

### 1. Test With Short Arrays First
Start with 2-3 items to test your configuration before creating long arrays

### 2. Use Consistent JSON Structure
All array items should have the same JSON structure:
```json
// ✅ Good - consistent structure
["{\"x\":100,\"y\":200}", "{\"x\":150,\"y\":250}"]

// ❌ Bad - inconsistent
["{\"x\":100,\"y\":200}", "{\"pos\":150}"]
```

### 3. Plan Your Array Length
Calculate how long your automation should run:
- Daily task for 1 week = 7 items
- Weekly task for 1 month = 4-5 items
- Monthly task for 1 year = 12 items

### 4. Use Clear Start Dates
Choose obvious start dates like:
- First day of month (2025-01-01)
- First day of week (Monday)
- Project start date

### 5. Document Your Arrays
Add a description to the node explaining:
- What the start date represents
- How many days the array covers
- What each value range means

## Future Dynamic Source Types

### Planned Features (In Development):

#### 🔄 Iteration-Based Array
- Values cycle through array based on execution frequency
- Each iteration uses next item in array
- Wraps around when array ends

#### 🌐 API Source
- Fetch values from REST API endpoints in real-time
- Support for GET/POST requests
- Custom headers and authentication
- JSONPath for value extraction

#### 📊 Excel/CSV File
- Read values from spreadsheet files
- Column/row selection
- Auto-update when file changes
- Support for formulas

#### ⚙️ Condition-Based Calculation
- Calculate values based on custom conditions
- If-then-else logic
- Compare dates, times, values
- Multiple condition support

#### 📆 Date Expression
- Calculate values using date/time formulas
- Relative dates (yesterday, next week, etc.)
- Date arithmetic (add/subtract days)
- Format customization

#### 💻 Custom Expression
- Use C# expressions for complex logic
- Access to system variables
- Mathematical operations
- String manipulation

**Note:** When you select any of these types, you'll see a "🚧 IN DEVELOPMENT" message with a description of the feature. Please use Date-Based Array until these are implemented.

## Troubleshooting

### "🚧 IN DEVELOPMENT" Message
**What it means:** You selected a dynamic source type that is not yet implemented  
**What to do:** Switch back to "Date-Based Array" which is fully functional  
**Status:** Other source types are being developed and will be available in future updates

### Values Not Changing
- Check that Value Mode is set to "Dynamic"
- Verify the start date is correct
- Ensure data array has valid JSON
- Check value mappings match your JSON structure

### Execution Stops Immediately
- Current date might be outside array range
- Check the error message for details
- Verify start date is not in the future
- Ensure array has enough items

### Wrong Values Being Used
- Verify the start date
- Count days from start to today
- Check that array index matches expected day
- Verify value mappings extract correct fields

## JSON Structure Reference

### Complete Node Configuration
```json
{
  "Type": "mouse_left_click",
  "ValueMode": "Dynamic",
  "DynamicSource": {
    "SourceType": "DateBasedArray",
    "StartDate": "2025-12-23",
    "DataArray": [
      "{\"x\":100,\"y\":200}",
      "{\"x\":150,\"y\":250}",
      "{\"x\":200,\"y\":300}"
    ],
    "ValueMappings": {
      "IntValues[0]": "$.x",
      "IntValues[1]": "$.y"
    }
  },
  "IntValues": [],
  "StringValues": [],
  "Frequency": 1,
  "DelayBefore": 0,
  "DelayAfter": 0,
  "Enabled": true
}
```

### Value Mapping Targets
- **IntValues[0]**, **IntValues[1]**, etc. → Integer parameters
- **StringValues[0]**, **StringValues[1]**, etc. → String parameters

### JSON Path Examples
- `$.x` → Top-level field "x"
- `$.position.x` → Nested field
- `$.data[0]` → Array item (not yet supported)

## Support

For issues or questions:
1. Check error messages carefully
2. Verify JSON syntax using a JSON validator
3. Test with simple examples first
4. Review this documentation

---

**Last Updated:** December 23, 2025  
**Version:** 1.0 - Initial Date-Based Array Implementation
