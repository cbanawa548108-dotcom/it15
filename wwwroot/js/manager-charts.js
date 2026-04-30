/**
 * Manager Dashboard Charts
 * CRLFruitstandESS
 */

const ManagerCharts = {
    instances: {},
    
    init: function(config) {
        this.config = config;
        this.initSalesChart('daily');
        this.initCategoryChart();
        this.initInventoryChart(30);
    },

    // ==================== SALES LINE CHART ====================
    initSalesChart: function(period) {
        const ctx = document.getElementById('salesChart');
        if (!ctx) return;
        
        if (this.instances.sales) {
            this.instances.sales.destroy();
        }

        const data = this.config.salesData || [];
        
        const labels = data.map(s => {
            const date = new Date(s.date);
            if (period === 'monthly') {
                return date.toLocaleDateString('en-US', { month: 'short', year: 'numeric' });
            }
            if (period === 'weekly') {
                return 'W' + this.getWeekNumber(date);
            }
            return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
        });

        const revenue = data.map(s => s.revenue);
        const transactions = data.map(s => s.transactionCount);

        const gradientRev = ctx.getContext('2d').createLinearGradient(0, 0, 0, 400);
        gradientRev.addColorStop(0, 'rgba(59, 130, 246, 0.3)');
        gradientRev.addColorStop(1, 'rgba(59, 130, 246, 0.0)');

        this.instances.sales = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Revenue (₱)',
                        data: revenue,
                        borderColor: '#3b82f6',
                        backgroundColor: gradientRev,
                        borderWidth: 3,
                        fill: true,
                        tension: 0.4,
                        pointBackgroundColor: '#3b82f6',
                        pointBorderColor: '#fff',
                        pointBorderWidth: 2,
                        pointRadius: 4,
                        yAxisID: 'y'
                    },
                    {
                        label: 'Transactions',
                        data: transactions,
                        borderColor: '#10b981',
                        backgroundColor: 'rgba(16, 185, 129, 0.1)',
                        borderWidth: 2,
                        borderDash: [5, 5],
                        fill: false,
                        tension: 0.4,
                        pointBackgroundColor: '#10b981',
                        pointRadius: 3,
                        yAxisID: 'y1'
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: {
                        position: 'top',
                        labels: { usePointStyle: true, padding: 20 }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(15, 23, 42, 0.9)',
                        titleColor: '#f8fafc',
                        bodyColor: '#e2e8f0',
                        borderColor: 'rgba(255,255,255,0.1)',
                        borderWidth: 1,
                        padding: 12,
                        callbacks: {
                            label: function(context) {
                                if (context.dataset.label.includes('Revenue')) {
                                    return context.dataset.label + ': ₱' + context.parsed.y.toLocaleString();
                                }
                                return context.dataset.label + ': ' + context.parsed.y;
                            }
                        }
                    }
                },
                scales: {
                    x: { grid: { display: false } },
                    y: {
                        type: 'linear',
                        display: true,
                        position: 'left',
                        grid: { color: 'rgba(255,255,255,0.05)' },
                        ticks: {
                            callback: function(value) {
                                return '₱' + (value / 1000).toFixed(0) + 'k';
                            }
                        }
                    },
                    y1: {
                        type: 'linear',
                        display: true,
                        position: 'right',
                        grid: { drawOnChartArea: false }
                    }
                }
            }
        });
    },

    switchSalesChart: function(period, btn) {
        document.querySelectorAll('.chart-btn').forEach(b => b.classList.remove('active'));
        if (btn) btn.classList.add('active');

        fetch(`/ManagerDashboard/GetSalesData?period=${period}`)
            .then(r => r.json())
            .then(data => {
                this.config.salesData = data;
                this.initSalesChart(period);
            });
    },

    // ==================== CATEGORY DOUGHNUT CHART ====================
    initCategoryChart: function() {
        const ctx = document.getElementById('categoryChart');
        if (!ctx) return;

        const data = this.config.categoryData || [];
        const colors = ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899', '#06b6d4', '#f97316'];

        new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: data.map(c => c.category),
                datasets: [{
                    data: data.map(c => c.revenue),
                    backgroundColor: colors,
                    borderColor: 'rgba(15, 23, 42, 0.8)',
                    borderWidth: 2,
                    hoverOffset: 10
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '60%',
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            padding: 15,
                            usePointStyle: true,
                            font: { size: 11 }
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(15, 23, 42, 0.9)',
                        callbacks: {
                            label: function(context) {
                                const val = context.parsed;
                                const total = context.dataset.data.reduce((a, b) => a + b, 0);
                                const pct = ((val / total) * 100).toFixed(1);
                                return ` ${context.label}: ₱${val.toLocaleString()} (${pct}%)`;
                            }
                        }
                    }
                }
            }
        });
    },

    // ==================== INVENTORY BAR CHART ====================
    initInventoryChart: function(days) {
        const ctx = document.getElementById('inventoryChart');
        if (!ctx) return;

        if (this.instances.inventory) {
            this.instances.inventory.destroy();
        }

        const movements = this.config.movementData || [];
        const dates = [];
        const stockInData = [];
        const stockOutData = [];

        for (let i = days - 1; i >= 0; i--) {
            const d = new Date();
            d.setDate(d.getDate() - i);
            const dateStr = d.toISOString().split('T')[0];
            dates.push(d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' }));

            const dayIn = movements
                .filter(m => m.type === 'Stock In' && m.date.startsWith(dateStr))
                .reduce((sum, m) => sum + m.quantity, 0);
            const dayOut = movements
                .filter(m => m.type === 'Stock Out' && m.date.startsWith(dateStr))
                .reduce((sum, m) => sum + m.quantity, 0);

            stockInData.push(dayIn);
            stockOutData.push(dayOut);
        }

        this.instances.inventory = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: dates,
                datasets: [
                    {
                        label: 'Stock In',
                        data: stockInData,
                        backgroundColor: 'rgba(16, 185, 129, 0.8)',
                        borderColor: '#10b981',
                        borderWidth: 1,
                        borderRadius: 4,
                        barPercentage: 0.7
                    },
                    {
                        label: 'Stock Out',
                        data: stockOutData,
                        backgroundColor: 'rgba(245, 158, 11, 0.8)',
                        borderColor: '#f59e0b',
                        borderWidth: 1,
                        borderRadius: 4,
                        barPercentage: 0.7
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'top',
                        labels: { usePointStyle: true }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(15, 23, 42, 0.9)',
                        callbacks: {
                            label: function(context) {
                                return ` ${context.dataset.label}: ${context.parsed.y} units`;
                            }
                        }
                    }
                },
                scales: {
                    x: { grid: { display: false } },
                    y: {
                        beginAtZero: true,
                        grid: { color: 'rgba(255,255,255,0.05)' },
                        ticks: { stepSize: 10 }
                    }
                }
            }
        });
    },

    switchInventoryChart: function(days, btn) {
        document.querySelectorAll('.chart-controls .chart-btn').forEach(b => b.classList.remove('active'));
        if (btn) btn.classList.add('active');

        fetch(`/ManagerDashboard/GetInventoryMovementData?days=${days}`)
            .then(r => r.json())
            .then(data => {
                // Reconstruct movement data from API response
                const movements = [];
                data.stockIn.forEach(item => {
                    movements.push({
                        type: 'Stock In',
                        date: item.date,
                        quantity: item.quantity
                    });
                });
                data.stockOut.forEach(item => {
                    movements.push({
                        type: 'Stock Out',
                        date: item.date,
                        quantity: item.quantity
                    });
                });
                this.config.movementData = movements;
                this.initInventoryChart(days);
            });
    },

    // ==================== HELPERS ====================
    getWeekNumber: function(date) {
        const d = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
        d.setUTCDate(d.getUTCDate() + 4 - (d.getUTCDay() || 7));
        const yearStart = new Date(Date.UTC(d.getUTCFullYear(), 0, 1));
        return Math.ceil((((d - yearStart) / 86400000) + 1) / 7);
    }
};

// Global functions for onclick handlers
function switchSalesChart(period, btn) {
    ManagerCharts.switchSalesChart(period, btn);
}

function switchInventoryChart(days, btn) {
    ManagerCharts.switchInventoryChart(days, btn);
}