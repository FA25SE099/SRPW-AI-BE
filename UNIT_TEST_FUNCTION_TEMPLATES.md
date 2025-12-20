# Unit Test Function Templates - Excel Ready Format

**Instructions:** 
1. Copy each function block below (from "Function Code" to "Defect ID")
2. Paste directly into Excel as tab-delimited text
3. The grid structure will automatically align into cells
4. Fill in the "Passed/Failed", "Executed Date", and "Defect ID" rows after testing

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
;;null;;;;O
;password
;;valid;O;;;;;O;O;O
;;wrong;;O
;;null;;;;O
;account_status
;;active;O;;;;;O;O;O
;;locked;;;O
;;unconfirmed;;;;O
;roles
;;single;O;;;;;O
;;multiple;;;;;;O
;;none

Confirm;Return
;token
;;valid_token;O;;;;;O;O;O
;;null;;O;O;O;O;O
;error_message
;;"Invalid credentials";;O;O
;;"Account locked";;;O
;;"Email not confirmed";;;;O
;;"Password required";;;;;O
;Exception

;Log message
;;"Login successful";O;;;;;O;O;O
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
;;unique;O
;;duplicate;;O
;;null;;;O
;cluster_id
;;valid;O
;;invalid;;;O
;;null;;;O
;excel_import
;;valid_data;;;;O
;;invalid_data;;;;;O
;;empty_file;;;;;O

Confirm;Return
;farmer_id
;;guid;O
;;null;;O;O
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
;;valid_value;O
;;zero;;O
;;negative;;O
;;within_tolerance;;;O
;;exceeds_tolerance;;;;O
;geojson
;;valid;O
;;invalid;;;;;O
;;malformed;;;;;O
;;self_intersecting;;;;;;;O
;;insufficient_points;;;;;;;;O
;plot_id
;;exists;O
;;not_found;;;;;;;;;;O
;sothua_soto
;;unique;O
;;duplicate;;O

Confirm;Return
;plot_id
;;guid;O
;;null;;O
;errors
;;"Plot already exists";;O
;;"Invalid area";;O
;;"Invalid GeoJSON";;;;;O
;;"Plot not found";;;;;;;;;;O
;Exception

;Log message
;;"Plot created successfully";O
;;"Polygon validated";;;O

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
;;valid;O
;;invalid;;;O
;;null;;;O
;season_id
;;valid;O
;;invalid;;;;O
;;null;;;;O
;eligible_plots
;;available;O
;;all_grouped;;O
;;no_cultivations;;;;;O
;proximity_threshold
;;default;O
;;custom;;;;;;O
;;zero;;;;;;O
;total_area
;;within_limits;O
;;below_minimum;;;;;;;;;O
;;above_maximum;;;;;;;;;O

Confirm;Return
;groups_list
;;with_groups;O
;;empty;;O
;;null;;O
;errors
;;"Cluster not found";;;O
;;"Season not found";;;;O
;;"No eligible plots";;O
;Exception

;Log message
;;"Groups formed successfully";O
;;"Custom parameters applied";;;;;;O

Result;Type(N: Normal, A: Abnormal, B: Boundary);N;A;A;A;A;N;N;A;B
;Passed/Failed;P;P;P;P;P;P;P;P;P
;Executed Date;12/13;12/13;12/13;12/13;12/13;12/13;12/13;12/13;12/13
;Defect ID
```	

---

## UT005 - Material Management

```
Function Code		UT005		Function Name				Material Management
Created By		<Developer Name>		Executed By				
Lines of code		140		Lack of test cases				-5
Test requirement		Material cost calculation, historical prices, create material
	Passed	Failed	Untested			N/A/B		Total Test Cases
	6	0	0			6	0	0	6

				MATERIAL-001	MATERIAL-002	MATERIAL-003	MATERIAL-004	MATERIAL-005	MATERIAL-006
Condition	Precondition										
													
													
	plot_area										
		valid	O									
		zero		O								
		negative		O								
													
	material_prices										
		exists	O									
		missing			O							
		historical				O						
		multiple_records				O						
													
	material_name										
		unique					O					
		duplicate						O				
		null						O				
													
													
Confirm	Return										
	cost_breakdown										
		with_details	O									
		null		O								
	warnings										
		"Missing price data"			O							
	material_id										
		guid					O					
		null						O				
	errors										
		"Material already exists"						O				
		"Area required"		O								
	Exception										
													
	Log message										
		"Cost calculated successfully"	O									
		"Material created"					O					
													
Result	Type(N: Normal, A: Abnormal, B: Boundary)		N	A	A	N	N	A
	Passed/Failed		P	P	P	P	P	P
	Executed Date		12/13	12/13	12/13	12/13	12/13	12/13
	Defect ID										
```	

---

## UT006 - UAV Service Management

```
Function Code		UT006		Function Name				UAV Service Management
Created By		<Developer Name>		Executed By				
Lines of code		170		Lack of test cases				-5
Test requirement		UAV plotting, order creation, vendor/manager validation
	Passed	Failed	Untested			N/A/B		Total Test Cases
	9	0	0			9	0	0	9

				UAV-001	UAV-002	UAV-003	UAV-004	UAV-005	UAV-006	UAV-007	UAV-008	UAV-009
Condition	Precondition												
														
														
	group_id												
		valid_with_plots	O											
		valid_empty			O									
		invalid		O										
		null		O										
														
	cluster_manager_id												
		valid				O								
		null					O							
														
	vendor_id												
		valid				O								
		invalid							O					
		null							O					
														
	group_total_area												
		valid				O								
		zero								O				
		negative								O				
														
	active_tasks												
		exists				O								
		none									O			
														
Confirm	Return												
	plots_list												
		with_plots	O											
		empty			O									
		null		O										
	order_id												
		guid				O								
		null		O										
	errors												
		"Group not found"		O										
		"Manager required"					O							
		"Vendor not found"							O					
		"Invalid area"								O				
	Exception												
														
	Log message												
		"UAV order created"				O								
		"Plots retrieved"	O											
														
Result	Type(N: Normal, A: Abnormal, B: Boundary)		N	A	N	N	A	A	A	B	A
	Passed/Failed		P	P	P	P	P	P	P	P	P
	Executed Date		12/13	12/13	12/13	12/13	12/13	12/13	12/13	12/13	12/13
	Defect ID												
```	

---

## UT007 - Farm Logging

```
Function Code		UT007		Function Name				Farm Logging
Created By		<Developer Name>		Executed By				
Lines of code		130		Lack of test cases				-5
Test requirement		Create farm log, materials and images, version validation
	Passed	Failed	Untested			N/A/B		Total Test Cases
	5	0	0			5	0	0	5

				FARMLOG-001	FARMLOG-002	FARMLOG-003	FARMLOG-004	FARMLOG-005
Condition	Precondition										
													
													
	cultivation_task_id										
		valid	O									
		invalid		O								
		null		O								
													
	version_id										
		matches	O									
		mismatch			O							
		null			O							
													
	materials_list										
		provided				O						
		empty				O						
		null				O						
													
	proof_images										
		provided					O					
		empty					O					
		null					O					
													
Confirm	Return										
	farmlog_id										
		guid	O									
		null		O								
	errors										
		"Task not found"		O								
		"Version conflict"			O							
	photo_urls										
		uploaded					O					
		null					O					
	Exception										
													
	Log message										
		"Farm log created"	O									
		"Images uploaded"					O					
													
Result	Type(N: Normal, A: Abnormal, B: Boundary)		N	A	A	N	N
	Passed/Failed		P	P	P	P	P
	Executed Date		12/13	12/13	12/13	12/13	12/13
	Defect ID										
```	

---

## UT008 - Rice Variety Management

```
Function Code		UT008		Function Name				Rice Variety Management
Created By		<Developer Name>		Executed By				
Lines of code		110		Lack of test cases				-5
Test requirement		Create/delete rice varieties, assign seasons
	Passed	Failed	Untested			N/A/B		Total Test Cases
	5	0	0			5	0	0	5

				RICE-001	RICE-002	RICE-003	RICE-004	RICE-005
Condition	Precondition										
													
													
	variety_name										
		unique	O									
		duplicate		O								
		null		O								
													
	variety_status										
		not_in_use	O									
		in_use			O							
													
	season_id										
		valid					O					
		invalid					O					
		null					O					
													
	query_filter										
		none				O						
		with_filter				O						
													
													
Confirm	Return										
	rice_variety_id										
		guid	O									
		null		O								
	varieties_list										
		with_results				O						
		empty				O						
	errors										
		"Variety already exists"		O								
		"Cannot delete in use"			O							
	Exception										
													
	Log message										
		"Variety created"	O									
		"Season changed"					O					
													
Result	Type(N: Normal, A: Abnormal, B: Boundary)		N	A	A	N	N
	Passed/Failed		P	P	P	P	P
	Executed Date		12/13	12/13	12/13	12/13	12/13
	Defect ID										
```	

---

## UT009 - Season Management

```
Function Code		UT009		Function Name				Season Management
Created By		<Developer Name>		Executed By				
Lines of code		115		Lack of test cases				-5
Test requirement		Create/delete seasons, year-season configs
	Passed	Failed	Untested			N/A/B		Total Test Cases
	5	0	0			5	0	0	5

				SEASON-001	SEASON-002	SEASON-003	SEASON-004	SEASON-005
Condition	Precondition										
													
													
	start_date										
		before_end_date	O									
		after_end_date		O								
		null		O								
													
	end_date										
		valid	O									
		before_start_date		O								
		null		O								
													
	season_overlap										
		no_overlap	O									
		overlaps		O								
													
	season_status										
		not_in_use	O									
		in_use			O							
													
	year_config										
		valid_year				O						
		existing_config				O						
													
													
Confirm	Return										
	season_id										
		guid	O									
		null		O								
	year_config_id										
		guid				O						
		null				O						
	errors										
		"Dates overlap"		O								
		"Season in use"			O							
	Exception										
													
	Log message										
		"Season created"	O									
		"Year config deleted"					O					
													
Result	Type(N: Normal, A: Abnormal, B: Boundary)		N	A	A	N	N
	Passed/Failed		P	P	P	P	P
	Executed Date		12/13	12/13	12/13	12/13	12/13
	Defect ID										
```	

---

## UT010 - Rice-Season Mapping

```
Function Code		UT010		Function Name				Rice-Season Mapping
Created By		<Developer Name>		Executed By				
Lines of code		80		Lack of test cases				-5
Test requirement		Map rice varieties to seasons
	Passed	Failed	Untested			N/A/B		Total Test Cases
	3	0	0			3	0	0	3

				RICE-SEASON-001	RICE-SEASON-002	RICE-SEASON-003
Condition	Precondition								
										
										
	variety_id								
		valid	O							
		invalid	O							
		null	O							
										
	season_id								
		valid	O							
		invalid	O							
		null	O							
										
	mapping_existence								
		new	O							
		duplicate		O						
		exists			O					
										
										
Confirm	Return								
	mapping_id								
		guid	O							
		null		O						
	errors								
		"Mapping exists"		O						
		"Invalid variety/season"		O						
	Exception								
										
	Log message								
		"Mapping created"	O							
		"Mapping deleted"			O					
										
Result	Type(N: Normal, A: Abnormal, B: Boundary)		N	A	N
	Passed/Failed		P	P	P
	Executed Date		12/13	12/13	12/13
	Defect ID								
```	

---

## UT011 - Expert Management

```
Function Code		UT011		Function Name				Expert Management
Created By		<Developer Name>		Executed By				
Lines of code		90		Lack of test cases				-5
Test requirement		Create agronomy expert accounts, validation
	Passed	Failed	Untested			N/A/B		Total Test Cases
	3	0	0			3	0	0	3

				EXPERT-001	EXPERT-002	EXPERT-003
Condition	Precondition								
										
										
	email								
		unique	O							
		duplicate		O						
		invalid		O						
		null		O						
										
	specialization								
		provided	O							
		missing			O					
		null			O					
										
	expert_status								
		active	O							
		inactive	O							
										
										
Confirm	Return								
	expert_id								
		guid	O							
		null		O						
	errors								
		"Email exists"		O						
		"Specialization required"			O					
	Exception								
										
	Log message								
		"Expert created"	O							
		"Invalid data"		O						
										
Result	Type(N: Normal, A: Abnormal, B: Boundary)		N	A	A
	Passed/Failed		P	P	P
	Executed Date		12/13	12/13	12/13
	Defect ID								
```	

---

## UT012 - Production Planning

```
Function Code		UT012		Function Name				Production Planning
Created By		<Developer Name>		Executed By				
Lines of code		160		Lack of test cases				-5
Test requirement		Create production plans, generate drafts, submit for approval
	Passed	Failed	Untested			N/A/B		Total Test Cases
	2	0	4			2	0	4	6

				PLAN-001	PLAN-002	PLAN-003	PLAN-004	PLAN-005	PLAN-006
Condition	Precondition									
												
												
	plot_cultivation_id									
		valid	O								
		invalid	O								
		null	O								
												
	standard_plan_id									
		exists					O				
		not_found						O			
		null					O				
												
	plan_status									
		draft	O								
		ready_for_submission			O					
		submitted			O					
		active				O				
												
	expert_id									
		valid		O							
		invalid		O							
		null		O							
												
	plan_data									
		complete	O								
		incomplete	O								
												
												
Confirm	Return									
	plan_id									
		guid	O								
		null	O								
	implementation_data									
		with_tasks				O				
		empty				O				
	errors									
		"PlotCultivation not found"	O								
		"Standard not found"						O			
		"Invalid expert"		O							
	Exception									
												
	Log message									
		"Plan created"	O								
		"Plan submitted"			O					
		"Draft generated"					O				
												
Result	Type(N: Normal, A: Abnormal, B: Boundary)		B	B	B	N	N	A
	Passed/Failed		P	P
	Executed Date		12/13	12/13
	Defect ID									
```	

---

## UT013 - Reporting & Emergency

```
Function Code		UT013		Function Name				Reporting & Emergency
Created By		<Developer Name>		Executed By				
Lines of code		125		Lack of test cases				-5
Test requirement		Create emergency reports, resolve reports, emergency plans
	Passed	Failed	Untested			N/A/B		Total Test Cases
	0	0	5			0	5	0	5

				REPORT-001	REPORT-002	REPORT-003	REPORT-004	REPORT-005
Condition	Precondition										
													
													
	plot_id										
		valid	O									
		invalid	O									
		null	O									
													
	report_status										
		new	O									
		pending		O								
		resolved				O						
													
	emergency_plan										
		required			O							
		not_required	O									
													
	resolution_text										
		provided				O						
		empty				O						
		null				O						
													
	proof_images										
		uploaded	O									
		missing					O					
		null					O					
													
													
Confirm	Return										
	report_id										
		guid	O									
		null		O								
	emergency_plan_id										
		guid			O							
		null			O							
	warnings										
		"Images recommended"					O					
	errors										
		"Plot not found"	O									
		"Report not found"		O								
	Exception										
													
	Log message										
		"Report created"	O									
		"Report resolved"		O								
													
Result	Type(N: Normal, A: Abnormal, B: Boundary)		N	A	A	N	A
	Passed/Failed										
	Executed Date										
	Defect ID										
```	

---

## UT014 - Standard Plan Management

```
Function Code		UT014		Function Name				Standard Plan Management
Created By		<Developer Name>		Executed By				
Lines of code		95		Lack of test cases				-5
Test requirement		Create/update standard plan templates, expert authorization
	Passed	Failed	Untested			N/A/B		Total Test Cases
	0	0	3			0	0	3	3

				STANDARD-001	STANDARD-002	STANDARD-003
Condition	Precondition								
										
										
	user_role								
		expert	O							
		non_expert		O						
		null		O						
										
	standard_plan_id								
		valid			O					
		invalid			O					
		null			O					
										
	template_data								
		complete	O							
		incomplete	O							
		null	O							
										
	authorization								
		authorized	O							
		unauthorized		O						
										
										
Confirm	Return								
	standard_plan_id								
		guid	O							
		null		O						
	errors								
		"Unauthorized"		O						
		"Invalid template"		O						
		"Plan not found"			O					
	Exception								
										
	Log message								
		"Plan created"	O							
		"Plan updated"			O					
										
Result	Type(N: Normal, A: Abnormal, B: Boundary)		N	A	N
	Passed/Failed								
	Executed Date								
	Defect ID								
```

---

## How to Use These Templates

### Copy to Excel:
1. **Select the entire code block** (between the ``` markers) for a function
2. **Copy** (Ctrl+C or Cmd+C)
3. **Open Excel** and select cell A1
4. **Paste** (Ctrl+V or Cmd+V) - Excel will automatically parse the tabs into columns
5. **Fill in** the "Passed/Failed", "Executed Date", and "Defect ID" rows after testing

### Excel Tips:
- The grid structure will automatically align to cells
- You can add borders to match the template image style
- Use conditional formatting to highlight failed tests
- The "O" marks in the grid show which test case covers which condition

### Notes:
- Replace `<Developer Name>` with actual developer name
- Update "Lines of code" with actual LOC from source control
- "Lack of test cases" shows gap between total possible and actual tests
- Mark cells with "O" for conditions tested by each test case
- Fill "Passed/Failed" row after test execution
- Record "Executed Date" when tests are run
- Add "Defect ID" if any issues found

---