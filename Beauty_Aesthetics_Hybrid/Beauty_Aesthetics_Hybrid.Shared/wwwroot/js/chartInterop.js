// wwwroot/js/chartInterop.js
window.chartInterop = {
    charts: {},

    createChart: function (id, data) {
        const canvas = document.getElementById(id);
        if (!canvas) {
            console.warn('Canvas not found:', id);
            return;
        }

        // Force canvas dimensions BEFORE creating chart
        canvas.style.width = '100%';
        canvas.style.height = '250px';
        canvas.width = canvas.offsetWidth;
        canvas.height = 250;

        // Destroy existing chart if it exists
        if (this.charts[id]) {
            this.charts[id].destroy();
            delete this.charts[id];
        }

        const ctx = canvas.getContext('2d');
        if (!ctx) return;

        this.charts[id] = new Chart(ctx, {
            type: 'line',
            data: {
                labels: data.map((_, i) => 'D' + (i + 1)),
                datasets: [{
                    data: data,
                    borderColor: '#0b66ff',
                    fill: true,
                    backgroundColor: 'rgba(11, 102, 255, 0.1)',
                    tension: 0.3,
                    pointRadius: 3,
                    pointHoverRadius: 5
                }]
            },
            options: {
                responsive: false, // CRITICAL: Disable responsive to prevent expansion
                maintainAspectRatio: false,
                scales: {
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: 'rgba(0, 0, 0, 0.05)'
                        },
                        ticks: {
                            padding: 8
                        }
                    },
                    x: {
                        grid: {
                            display: false
                        },
                        ticks: {
                            padding: 8
                        }
                    }
                },
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        mode: 'index',
                        intersect: false,
                        backgroundColor: 'rgba(0, 0, 0, 0.8)',
                        padding: 12,
                        cornerRadius: 8
                    }
                },
                interaction: {
                    mode: 'nearest',
                    axis: 'x',
                    intersect: false
                }
            }
        });
    },

    updateChart: function (id, data) {
        const chart = this.charts[id];

        // If chart doesn't exist, create it
        if (!chart) {
            console.warn('Chart not found, creating new one:', id);
            return this.createChart(id, data);
        }

        // Update existing chart data
        try {
            chart.data.datasets[0].data = data;
            chart.data.labels = data.map((_, i) => 'D' + (i + 1));
            chart.update('none'); // Update without animation
        } catch (error) {
            console.error('Error updating chart:', id, error);
            // If update fails, try recreating the chart
            this.createChart(id, data);
        }
    },

    destroyChart: function (id) {
        if (this.charts[id]) {
            this.charts[id].destroy();
            delete this.charts[id];
        }
    },

    destroyAllCharts: function () {
        Object.keys(this.charts).forEach(id => {
            if (this.charts[id]) {
                this.charts[id].destroy();
            }
        });
        this.charts = {};
    }
};