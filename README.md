# Dummy data + plotting for NeuroProstheticSuite figures

This folder contains scripts to generate dummy CSV data for 12 graphs (designed to match the 12 figures/graphs described in the project) and a plotting script to render them as PNGs.

Structure:
- scripts/generate_dummy_data.py  -> generates CSV files under `data/`
- scripts/plot_graphs.py         -> reads CSV files under `data/` and writes PNGs to `output/`
- requirements.txt               -> Python packages required

How to run (recommended):
1. Create a Python virtual environment:
   python -m venv .venv
   source .venv/bin/activate   # Linux/Mac
   .venv\Scripts\activate      # Windows PowerShell

2. Install packages:
   pip install -r requirements.txt

3. Generate dummy CSV files:
   python scripts/generate_dummy_data.py

4. Create PNGs from the CSV files:
   python scripts/plot_graphs.py

Outputs:
- data/Graph01_GripForce.csv ... data/Graph12_RMSE_Comparison.csv
- output/Graph01_GripForce.png ... output/Graph12_RMSE.png

Notes:
- These are synthetic placeholders. Replace CSV contents with your real processed data when available.
- The plotting script uses matplotlib with dark theme; feel free to adjust styles or switch to another plotting library (LiveCharts / OxyPlot) if you want integration into the WPF app.
