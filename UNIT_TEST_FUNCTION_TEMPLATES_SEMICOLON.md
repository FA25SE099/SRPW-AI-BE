# Unit Test Function Templates - Semicolon Delimited for Excel

**Instructions:** 
1. Copy each function block below (between the ``` marks)
2. Paste into Excel
3. Select the pasted cells
4. Go to Data → Text to Columns
5. Choose "Delimited" → Click Next
6. Select "Semicolon" as delimiter → Click Finish
7. The grid will align perfectly into columns

---

## UT001 - Authentication & Authorization

```
Function Code;UT001;Function Name;Authentication & Authorization
Created By;<Developer Name>;Executed By;
Lines of code;150;Lack of test cases;-5
Test requirement;Login, role resolution, 2FA, account states, validation
;Passed;Failed;Untested;N/A/B;Total Test Cases
;9;0;0;9;0;0;9

Condition;Precondition;AUTH-001;AUTH-002;AUTH-003;AUTH-004;AUTH-005;AUTH-006;AUTH-007;AUTH-008;AUTH-009
;email
;;valid;O;;;;;;;O;O
;;invalid;;O
;;null;;;;;;O
;password
;;valid;O;;;;;;;O;O
;;wrong;;;O
;;null;;;;;;O
;account_status
;;active;O;;;;;;;O;O
;;locked;;;;O
;;unconfirmed;;;;;O
;roles
;;single;O;;;;;;;O
;;multiple;;;;;;;O
;;none

Confirm;Return
;token
;;valid_token;O;;;;;;;O;O
;;null;;O;O;O;O;O
;error_message
;;"Invalid credentials";;O;O
;;"Account locked";;;;O
;;"Email not confirmed";;;;;O
;;"Password required";;;;;;O
;Exception

;Log message
;;"Login successful";O;;;;;;;O;O
;;"Login failed";;O;O;O;O;O

Result;Type(N: Normal, A: Abnormal, B: Boundary);N;A;A;A;A;A;N;N;N
;Passed/Failed;P;P;P;P;P;P;P;P;P
;Executed Date;12/13;12/13;12/13;12/13;12/13;12/13;12/13;12/13;12/13
;Defect ID
```

---

## UT002 - Farmer Management

```
Function Code;UT002;Function Name;Farmer Management
Created By;<Developer Name>;Executed By;
Lines of code;120;Lack of test cases;-5
Test requirement;Create farmer, duplicate checks, import from Excel
;Passed;Failed;Untested;N/A/B;Total Test Cases
;5;0;0;5;0;0;5

Condition;Precondition;FARMER-001;FARMER-002;FARMER-003;FARMER-004;FARMER-005
;phone_number
;;unique;O;;;O
;;duplicate;;O
;;null;;;;O
;cluster_id
;;valid;O;;O;O
;;invalid;;;O
;;null
;excel_import
;;valid_data;;;;O
;;invalid_data;;;;;O
;;empty_file;;;;;O

Confirm;Return
;farmer_id
;;guid;O;;;O
;;null;;O;O;;O
;errors
;;validation_list;;;;;O
;;"Phone already exists";;O
;;"Cluster not found";;;O
;Exception

;Log message
;;"Farmer created";O
;;"Import completed";;;;O

Result;Type(N: Normal, A: Abnormal, B: Boundary);N;A;A;N;A
;Passed/Failed;P;P;P;P;P
;Executed Date;12/13;12/13;12/13;12/13;12/13
;Defect ID
```

---

## UT003 - Plot Management

```
Function Code;UT003;Function Name;Plot Management
Created By;<Developer Name>;Executed By;
Lines of code;200;Lack of test cases;-5
Test requirement;Plot creation, polygon validation, area checks
;Passed;Failed;Untested;N/A/B;Total Test Cases
;12;0;0;12;0;0;12

Condition;Precondition;PLOT-001;PLOT-002;PLOT-003;PLOT-004;PLOT-005;PLOT-006;PLOT-007;PLOT-008;PLOT-009;PLOT-010;PLOT-011;PLOT-012
;area
;;valid_value;O;;;O;;;;;;O
;;zero;;;O
;;negative;;;O
;;within_tolerance;;;;O
;;exceeds_tolerance;;;;;O
;geojson
;;valid;O;;;O;;;;;;O
;;invalid;;;;;;O
;;malformed;;;;;;O
;;self_intersecting;;;;;;;;O
;;insufficient_points;;;;;;;;;O
;plot_id
;;exists;O;;;O;;;;;O
;;not_found;;;;;;;O;;;O
;sothua_soto
;;unique;O
;;duplicate;;O

Confirm;Return
;plot_id
;;guid;O;;;;;;;;;O
;;null;;O;O;;;;;;;;;O
;errors
;;"Plot already exists";;O
;;"Invalid area";;;O
;;"Invalid GeoJSON";;;;;;O;O
;;"Plot not found";;;;;;;O;;;O
;Exception

;Log message
;;"Plot created successfully";O
;;"Polygon validated";;;;O;O

Result;Type(N: Normal, A: Abnormal, B: Boundary);N;A;A;N;A;A;A;N;B;N;A;A
;Passed/Failed;P;P;P;P;P;P;P;P;P;P;P;P
;Executed Date;12/13;12/13;12/13;12/13;12/13;12/13;12/13;12/13;12/13;12/13;12/13;12/13
;Defect ID
```

---

## UT004 - Group Formation

```
Function Code;UT004;Function Name;Group Formation
Created By;<Developer Name>;Executed By;
Lines of code;180;Lack of test cases;-5
Test requirement;Group formation algorithm, parameters, edge cases
;Passed;Failed;Untested;N/A/B;Total Test Cases
;9;0;0;9;0;0;9

Condition;Precondition;GROUP-001;GROUP-002;GROUP-003;GROUP-004;GROUP-005;GROUP-006;GROUP-007;GROUP-008;GROUP-009
;cluster_id
;;valid;O;O;;;O;O;O;O
;;invalid;;;O;O
;;null
;season_id
;;valid;O;O;;;O;O;O;O
;;invalid;;;;O
;;null
;eligible_plots
;;available;O;;;;O;O;O
;;all_grouped;;O;;;;O
;;no_cultivations;;;;;O
;proximity_threshold
;;default;O;O;;;;O;O;O
;;custom;;;;;;O
;;zero;;;;;;O
;total_area
;;within_limits;O;O;;;;O
;;below_minimum;;;;;;;;;O
;;above_maximum;;;;;;;;;;O

Confirm;Return
;groups_list
;;with_groups;O;;;;O;O
;;empty;;O
;errors
;;"Cluster not found";;;O;O
;;"Season not found";;;;O
;;"No eligible plots";;O;;;O
;;"Plots already grouped";;;;;;;;O
;;"Insufficient area";;;;;;;;;O
;Exception

;Log message
;;"Groups formed successfully";O;;;;O
;;"Custom parameters applied";;;;;;O

Result;Type(N: Normal, A: Abnormal, B: Boundary);N;A;A;A;A;N;N;A;B
;Passed/Failed;P;P;P;P;P;P;P;P;P
;Executed Date;12/13;12/13;12/13;12/13;12/13;12/13;12/13;12/13;12/13
;Defect ID
```

---

## UT005 - Material Management

```
Function Code;UT005;Function Name;Material Management
Created By;<Developer Name>;Executed By;
Lines of code;140;Lack of test cases;-5
Test requirement;Material cost calculation, historical prices, create material
;Passed;Failed;Untested;N/A/B;Total Test Cases
;6;0;0;6;0;0;6

Condition;Precondition;MATERIAL-001;MATERIAL-002;MATERIAL-003;MATERIAL-004;MATERIAL-005;MATERIAL-006
;plot_area
;;valid;O;;;O;O
;;zero;;O
;;negative;;;O
;material_prices
;;exists;O
;;missing;;;;O
;;historical;;;;;O
;;multiple_records;;;;;O
;material_name
;;unique;;;;;;O
;;duplicate;;;;;;;O
;;null

Confirm;Return
;cost_breakdown
;;with_details;O;;;O
;;null;;O;O;;;O
;warnings
;;"Missing price data";;;;O
;material_id
;;guid;;;;;;O
;;null;;;;;;;O
;errors
;;"Material already exists";;;;;;;O
;;"Area required";;O;O
;Exception

;Log message
;;"Cost calculated successfully";O;;;O
;;"Material created";;;;;;O

Result;Type(N: Normal, A: Abnormal, B: Boundary);N;A;A;N;N;A
;Passed/Failed;P;P;P;P;P;P
;Executed Date;12/13;12/13;12/13;12/13;12/13;12/13
;Defect ID
```

---

## UT006 - UAV Service Management

```
Function Code;UT006;Function Name;UAV Service Management
Created By;<Developer Name>;Executed By;
Lines of code;170;Lack of test cases;-5
Test requirement;UAV plotting, order creation, vendor/manager validation
;Passed;Failed;Untested;N/A/B;Total Test Cases
;9;0;0;9;0;0;9

Condition;Precondition;UAV-001;UAV-002;UAV-003;UAV-004;UAV-005;UAV-006;UAV-007;UAV-008;UAV-009
;group_id
;;valid_with_plots;O;;;;;;;;O
;;valid_empty;;O
;;invalid;;;O;;O;O;O
;;null;;;;;;;;O
;cluster_manager_id
;;valid;O;;;O;;;;O
;;null;;;;;O
;vendor_id
;;valid;O;;;O;;;;O
;;invalid;;;;;;;O
;;null;;;;;;;;
;group_total_area
;;valid;O;;O;O;;;;O
;;zero;;;;;;;;O
;;negative;;;;;;;;O
;active_tasks
;;exists;O;;;O
;;none;;;;;;;;;O

Confirm;Return
;order_id
;;guid;O;;;O
;;null;;O;O;;O;O;O;O
;plot_selection
;;with_plots;O
;;empty;;O
;errors
;;"Group not found";;;O;;O
;;"Invalid vendor";;;;;;;O
;;"Manager required";;;;;O
;;"Invalid area";;;;;;;;O
;;"No active tasks";;;;;;;;;O
;Exception

;Log message
;;"Order created";O;;;O
;;"Plots selected";O

Result;Type(N: Normal, A: Abnormal, B: Boundary);N;A;A;A;A;A;A;B;B
;Passed/Failed;P;P;P;P;P;P;P;P;P
;Executed Date;12/13;12/13;12/13;12/13;12/13;12/13;12/13;12/13;12/13
;Defect ID
```

---

## UT007 - Farm Logging

```
Function Code;UT007;Function Name;Farm Logging
Created By;<Developer Name>;Executed By;
Lines of code;130;Lack of test cases;-5
Test requirement;Farm log creation, version conflicts, material tracking
;Passed;Failed;Untested;N/A/B;Total Test Cases
;5;0;0;5;0;0;5

Condition;Precondition;FARMLOG-001;FARMLOG-002;FARMLOG-003;FARMLOG-004;FARMLOG-005
;cultivation_task_id
;;valid;O;;;O;O
;;invalid;;O
;;null;;;O
;version_id
;;matches;O;;;O;O
;;mismatch;;;O
;;null
;materials_list
;;provided;O;;;O
;;empty;O
;;null;O
;proof_images
;;provided;O;;;;O
;;empty;O
;;null;O;;;O

Confirm;Return
;farmlog_id
;;guid;O;;;O;O
;;null;;O;O
;errors
;;"Task not found";;O
;;"Version conflict";;;O
;photo_urls
;;uploaded;O;;;;O
;;null;;O;O;O

Result;Type(N: Normal, A: Abnormal, B: Boundary);N;A;A;N;N
;Passed/Failed;P;P;P;P;P
;Executed Date;12/13;12/13;12/13;12/13;12/13
;Defect ID
```

---

## UT008 - Rice Variety Management

```
Function Code;UT008;Function Name;Rice Variety Management
Created By;<Developer Name>;Executed By;
Lines of code;110;Lack of test cases;-5
Test requirement;Create/delete rice varieties, assign seasons
;Passed;Failed;Untested;N/A/B;Total Test Cases
;5;0;0;5;0;0;5

Condition;Precondition;RICE-001;RICE-002;RICE-003;RICE-004;RICE-005
;variety_name
;;unique;O;;;;O
;;duplicate;;O
;;null
;variety_status
;;not_in_use;O
;;in_use;;;O
;season_id
;;valid;;;;;O
;;invalid
;;null
;query_filter
;;none;;;;O
;;with_filter;;;;O

Confirm;Return
;rice_variety_id
;;guid;O;;;;O
;;null;;O;O
;varieties_list
;;with_results;;;;O
;;empty
;errors
;;"Variety already exists";;O
;;"Cannot delete in use";;;O
;Exception

;Log message
;;"Variety created";O
;;"Season changed";;;;;O

Result;Type(N: Normal, A: Abnormal, B: Boundary);N;A;A;N;N
;Passed/Failed;P;P;P;P;P
;Executed Date;12/13;12/13;12/13;12/13;12/13
;Defect ID
```

---

## UT009 - Season Management

```
Function Code;UT009;Function Name;Season Management
Created By;<Developer Name>;Executed By;
Lines of code;115;Lack of test cases;-5
Test requirement;Create/delete seasons, year-season configs
;Passed;Failed;Untested;N/A/B;Total Test Cases
;5;0;0;5;0;0;5

Condition;Precondition;SEASON-001;SEASON-002;SEASON-003;SEASON-004;SEASON-005
;start_date
;;before_end_date;O;;;O
;;after_end_date;;O
;;null
;end_date
;;valid;O;;;O
;;before_start_date;;O
;;null
;season_overlap
;;no_overlap;O;;;O
;;overlaps;;O
;season_status
;;not_in_use;O;;;O;O
;;in_use;;;O
;year_config
;;valid_year;;;;O
;;existing_config

Confirm;Return
;season_id
;;guid;O;;;O
;;null;;O;O;;O
;year_config_id
;;guid;;;;O
;;null
;errors
;;"Dates overlap";;O
;;"Season in use";;;O
;Exception

;Log message
;;"Season created";O
;;"Year config created";;;;O
;;"Year config deleted";;;;;O

Result;Type(N: Normal, A: Abnormal, B: Boundary);N;A;A;N;N
;Passed/Failed;P;P;P;P;P
;Executed Date;12/13;12/13;12/13;12/13;12/13
;Defect ID
```

---

## UT010 - Rice-Season Mapping

```
Function Code;UT010;Function Name;Rice-Season Mapping
Created By;<Developer Name>;Executed By;
Lines of code;80;Lack of test cases;-5
Test requirement;Map rice varieties to seasons
;Passed;Failed;Untested;N/A/B;Total Test Cases
;3;0;0;3;0;0;3

Condition;Precondition;RICE-SEASON-001;RICE-SEASON-002;RICE-SEASON-003
;variety_id
;;valid;O;;O
;;invalid
;;null
;season_id
;;valid;O;;O
;;invalid
;;null
;mapping_existence
;;new;O
;;duplicate;;O
;;exists;;;O

Confirm;Return
;mapping_id
;;guid;O
;;null;;O;O
;errors
;;"Mapping exists";;O
;;"Invalid variety/season"
;Exception

;Log message
;;"Mapping created";O
;;"Mapping deleted";;;O

Result;Type(N: Normal, A: Abnormal, B: Boundary);N;A;N
;Passed/Failed;P;P;P
;Executed Date;12/13;12/13;12/13
;Defect ID
```

---

## UT011 - Expert Management

```
Function Code;UT011;Function Name;Expert Management
Created By;<Developer Name>;Executed By;
Lines of code;90;Lack of test cases;-5
Test requirement;Create agronomy expert accounts, validation
;Passed;Failed;Untested;N/A/B;Total Test Cases
;3;0;0;3;0;0;3

Condition;Precondition;EXPERT-001;EXPERT-002;EXPERT-003
;email
;;unique;O
;;duplicate;;O
;;invalid
;;null
;specialization
;;provided;O
;;missing;;;O
;;null
;expert_status
;;active;O
;;inactive

Confirm;Return
;expert_id
;;guid;O
;;null;;O;O
;errors
;;"Email exists";;O
;;"Specialization required";;;O
;Exception

;Log message
;;"Expert created";O
;;"Invalid data";;O

Result;Type(N: Normal, A: Abnormal, B: Boundary);N;A;A
;Passed/Failed;P;P;P
;Executed Date;12/13;12/13;12/13
;Defect ID
```

---

## UT012 - Production Planning

```
Function Code;UT012;Function Name;Production Planning
Created By;<Developer Name>;Executed By;
Lines of code;160;Lack of test cases;-5
Test requirement;Create production plans, generate drafts, submit for approval
;Passed;Failed;Untested;N/A/B;Total Test Cases
;2;0;4;2;0;4;6

Condition;Precondition;PLAN-001;PLAN-002;PLAN-003;PLAN-004;PLAN-005;PLAN-006
;plot_cultivation_id
;;valid;O;;O;O;O
;;invalid
;;null
;standard_plan_id
;;exists;;O;;;;O
;;not_found;;;;;;O
;;null
;plan_status
;;draft
;;ready_for_submission;;;O
;;submitted
;;active;;;;O
;expert_id
;;valid;;O
;;invalid
;;null
;plan_data
;;complete;;O
;;incomplete

Confirm;Return
;plan_id
;;guid;O;O
;;null
;implementation_data
;;with_tasks;;;;O
;;empty
;errors
;;"PlotCultivation not found"
;;"Standard not found";;;;;;O
;;"Invalid expert"
;Exception

;Log message
;;"Plan created";O;O
;;"Plan submitted";;;O
;;"Draft generated";;;;;O
;;"Implementation retrieved";;;;O

Result;Type(N: Normal, A: Abnormal, B: Boundary);B;B;B;N;N;A
;Passed/Failed;P;P
;Executed Date;12/13;12/13
;Defect ID
```

---

## UT013 - Reporting & Emergency

```
Function Code;UT013;Function Name;Reporting & Emergency
Created By;<Developer Name>;Executed By;
Lines of code;125;Lack of test cases;-5
Test requirement;Create emergency reports, resolve reports, emergency plans
;Passed;Failed;Untested;N/A/B;Total Test Cases
;0;0;5;0;5;0;5

Condition;Precondition;REPORT-001;REPORT-002;REPORT-003;REPORT-004;REPORT-005
;plot_id
;;valid;O;;O;O;O
;;invalid
;;null
;report_status
;;new;O
;;pending;;O
;;resolved;;;;O
;emergency_plan
;;required;;;O
;;not_required;O
;resolution_text
;;provided;;;;O
;;empty
;;null
;proof_images
;;uploaded;O
;;missing;;;;;O
;;null

Confirm;Return
;report_id
;;guid;O;;O;;O
;;null
;emergency_plan_id
;;guid;;;O
;;null
;warnings
;;"Images recommended";;;;;O
;errors
;;"Plot not found"
;;"Report not found";;O
;Exception

;Log message
;;"Report created";O
;;"Report resolved";;O;;;O
;;"Emergency plan created";;;O

Result;Type(N: Normal, A: Abnormal, B: Boundary);N;A;A;N;A
;Passed/Failed
;Executed Date
;Defect ID
```

---

## UT014 - Standard Plan Management

```
Function Code;UT014;Function Name;Standard Plan Management
Created By;<Developer Name>;Executed By;
Lines of code;95;Lack of test cases;-5
Test requirement;Create/update standard plan templates, expert authorization
;Passed;Failed;Untested;N/A/B;Total Test Cases
;0;0;3;0;0;3;3

Condition;Precondition;STANDARD-001;STANDARD-002;STANDARD-003
;user_role
;;expert;O;;O
;;non_expert;;O
;;null
;standard_plan_id
;;valid;;;O
;;invalid
;;null
;template_data
;;complete;O;;O
;;incomplete
;;null
;authorization
;;authorized;O;;O
;;unauthorized;;O

Confirm;Return
;standard_plan_id
;;guid;O;;O
;;null;;O
;errors
;;"Unauthorized";;O
;;"Invalid template"
;;"Plan not found"
;Exception

;Log message
;;"Plan created";O
;;"Plan updated";;;O

Result;Type(N: Normal, A: Abnormal, B: Boundary);N;A;N
;Passed/Failed
;Executed Date
;Defect ID
```

---

## How to Use These Templates in Excel

1. **Copy the template**: Copy the entire block between the ``` marks for the function you want
2. **Paste into Excel**: Paste into cell A1 (or any starting cell)
3. **Convert to columns**:
   - Select all the pasted cells
   - Go to the **Data** tab in Excel ribbon
   - Click **Text to Columns**
   - Choose **Delimited** → Click **Next**
   - Check **Semicolon** → Click **Finish**
4. **Result**: Your data will be perfectly aligned in columns!

Each "O" marker will be in the exact column for its corresponding test case ID.
