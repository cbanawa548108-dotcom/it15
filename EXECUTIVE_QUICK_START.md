# Executive Decision Support System - Quick Start Guide

## Getting Started

### 1. **Access the Executive Dashboard**
- Navigate to: `http://localhost:5000/ExecutiveDashboard`
- Log in with CFO, CEO, or Admin credentials
- The main dashboard displays real-time KPIs and business health

### 2. **Main Dashboard Features**

#### KPI Overview
- **Total Revenue**: Sum of all sales for current month
- **Total Expenses**: Operating costs
- **Net Profit**: Revenue minus expenses
- **Gross Margin %**: Profitability percentage
- **Operational Efficiency**: Cost efficiency ratio
- **ROI**: Return on invested capital

#### Business Health Score
- Displayed as a score 0-100
- Factors in profitability, margins, efficiency, and risks
- Accompanied by status (Healthy/Warning/Critical)

#### Active Alerts
- Automatic system alerts for critical events
- Click "Acknowledge" to mark as resolved
- Color-coded by severity (Critical/Warning/Info)

---

## 3. **KPI Dashboard**

**Route**: `/ExecutiveDashboard/KPIDashboard`

### Features:
- Monthly performance metrics
- Navigate between months using "Previous/Next" buttons
- View:
  - Current month KPIs
  - Previous month comparison
  - Month-over-month change percentages
  - Status indicators for each metric

### How to Use:
1. Select a month using navigation buttons
2. Review KPI cards for each metric
3. Compare current vs previous month
4. Identify trends (green=good, orange=warning, red=danger)

---

## 4. **Sales Forecasting**

**Route**: `/ExecutiveDashboard/Forecasting`

### Features:
- 30-day revenue forecast
- Three forecasting methods:
  - **Moving Average**: Good for stable trends
  - **Exponential Smoothing**: Weights recent data
  - **Linear Regression**: Trend-based projection

### How to Use:
1. View forecast summary cards
2. Compare accuracy metrics (MAPE%)
3. Review confidence intervals table
4. Use forecast for:
   - Resource planning
   - Cash flow management
   - Setting sales targets

### Key Metrics:
- **MAPE**: Mean Absolute Percentage Error (lower is better)
- **Confidence**: Probability of forecast accuracy
- **Bounds**: Upper/lower range of prediction

---

## 5. **What-If Scenario Analysis**

**Route**: `/ExecutiveDashboard/ScenarioSimulation`

### Create a Scenario:
1. Enter parameters:
   - Revenue growth rate (%)
   - Expense growth rate (%)
   - COGS percentage
   - Simulation period (months)

2. Click "Run Simulation"

### Review Results:
- **Total Projected Revenue**: Sum of all months
- **Total Projected Profit**: Net profit projection
- **Average Profit Margin**: Average margin %
- **ROI**: Return on investment %

### Sensitivity Analysis:
- Shows impact of ±10% changes in key variables
- Identify highest-impact variables
- Use for strategic planning

### Save Scenarios:
- Name your scenario (e.g., "Growth Strategy")
- Add optional description
- Click "Save Scenario"
- Compare multiple scenarios later

---

## 6. **Risk Analysis Dashboard**

**Route**: `/ExecutiveDashboard/RiskAnalysis`

### Overall Risk Indicators:
- **Risk Score**: 0-1 scale (0=low, 1=high)
- **Risk Status**: Low/Medium/High/Critical
- **Highest Risk Category**: Most pressing concern

### Risk Categories:
1. **Revenue Risk**: Sales volatility
2. **Operational Risk**: Expense fluctuations
3. **Profitability Risk**: Profit margin pressure
4. **Liquidity Risk**: Cash availability
5. **External Risk**: Market/seasonal factors

### For Each Risk:
- Probability: Likelihood of occurrence
- Impact: Severity if occurs
- Volatility: Data variability
- Insights: Specific findings

### Color Coding:
- 🟢 Green: Low risk
- 🟡 Yellow: Medium risk
- 🔴 Red: High risk

### How to Use:
1. Review overall risk score
2. Identify highest risks
3. Read insights for each risk
4. Use risk matrix to visualize
5. Plan mitigation strategies

---

## 7. **Decision Support Dashboard**

**Route**: `/ExecutiveDashboard/DecisionSupport`

### Comprehensive Analysis:
Combines all system data for strategic decisions:
- Current KPI status
- Health score with insights
- Active risks
- Revenue forecast
- AI-generated recommendations

### Recommendations Include:
- Business health assessment
- Risk warnings
- Strategic action items
- Monitoring guidelines

---

## 8. **API Access for Integration**

### Base URL:
`https://localhost/api/executive`

### Common Endpoints:

#### Get Today's KPIs:
```
GET /kpi/today
```

#### Get KPI Comparison:
```
GET /kpi/comparison
```

#### Get Business Health:
```
GET /health-score
```

#### Get Current Risks:
```
GET /risks/current
```

#### Get Revenue Forecast:
```
GET /forecast/revenue?forecastDays=30
```

#### Run Scenario:
```
POST /scenario/project
Body: {
  "baselineRevenue": 1000000,
  "baselineExpenses": 600000,
  "revenueGrowthPercent": 5,
  "expenseGrowthPercent": 3,
  "cogsPercent": 30,
  "simulationMonths": 12
}
```

### Authentication:
All API calls require Bearer token:
```
Authorization: Bearer {your_auth_token}
```

---

## 9. **Tips & Best Practices**

### For Accurate Forecasting:
- Maintain minimum 7 days of transaction history
- Update data daily for best results
- Review forecast monthly and adjust as needed

### For Risk Analysis:
- Check alerts daily
- Review risk dashboard weekly
- Act on critical alerts immediately

### For Scenario Planning:
- Create multiple scenarios for comparison
- Test different growth strategies
- Save scenarios for historical comparison

### For Decision Making:
- Use Decision Support dashboard daily
- Cross-reference multiple indicators
- Review trends over time
- Share insights with leadership team

---

## 10. **Key Metrics Explained**

| Metric | Definition | Target |
|--------|-----------|--------|
| Gross Margin % | (Revenue - COGS) / Revenue | > 30% |
| Operational Efficiency | (Revenue - Expenses) / Revenue | > 70% |
| ROI | (Profit / Expenses) × 100 | > 20% |
| Cash Flow Ratio | Revenue / Expenses | > 1.0 |
| Health Score | Composite business health | > 70 |
| Risk Score | Business risk level | < 0.5 |

---

## 11. **Common Questions**

**Q: How often is data updated?**
A: Data is real-time from the database. Refresh your browser to see latest changes.

**Q: Can I export reports?**
A: Charts can be screenshot/saved. Future versions will support PDF export.

**Q: What if I see "Insufficient Data"?**
A: You need at least 7 days of historical transaction data.

**Q: How are forecasts calculated?**
A: Using exponential smoothing (weighted average) based on 90-day history.

**Q: Can I customize KPI targets?**
A: KPI targets are set in the system. Contact admin to adjust.

---

## 12. **Troubleshooting**

### Problem: Dashboard not loading
- Clear browser cache
- Check internet connection
- Verify login credentials

### Problem: No alerts showing
- Check if alerts are resolved
- Review Risk Analysis page instead
- Verify user permissions

### Problem: Forecast accuracy low
- Ensure sufficient historical data
- Check for data anomalies
- Review seasonal patterns

### Problem: Can't save scenarios
- Verify scenario name is entered
- Check if all parameters are valid
- Review system permissions

---

## 13. **System Requirements**

- **Browser**: Chrome, Firefox, Safari, Edge (latest)
- **Screen Size**: Optimized for 1024px+ width
- **Network**: Stable internet connection
- **Authentication**: Valid CFO/CEO/Admin credentials

---

## 14. **Support Resources**

- **Documentation**: See EXECUTIVE_DASHBOARD_GUIDE.md
- **API Reference**: Check API controller comments
- **Database Queries**: Review service implementations
- **UI Issues**: Check executive-dashboard.css

---

## 15. **Daily Workflow**

### Morning (Start of Day):
1. Login to Executive Dashboard
2. Review overnight alerts
3. Check current KPIs vs targets
4. Note any anomalies

### Mid-Day (Monitoring):
1. Update scenarios if needed
2. Review new alerts
3. Check forecast vs actuals
4. Brief leadership on changes

### End of Day (Analysis):
1. Generate Decision Support summary
2. Save any new scenarios
3. Document findings
4. Plan next day actions

---

**Version**: 1.0  
**Last Updated**: April 27, 2026  

For detailed technical information, see EXECUTIVE_DASHBOARD_GUIDE.md
