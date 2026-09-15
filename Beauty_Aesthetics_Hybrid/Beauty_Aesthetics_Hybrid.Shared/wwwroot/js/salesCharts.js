// Sales Charts Module
window.salesCharts = (function () {
    let pieChart = null;
    let barChart = null;

    function createPieChart(canvasId, labels, data, colors) {
        // Destroy existing chart if it exists
        if (pieChart) {
            pieChart.destroy();
        }

        const ctx = document.getElementById(canvasId);
        if (!ctx) {
            console.error(`Canvas element with id '${canvasId}' not found`);
            return;
        }

        const hasData = data.some(value => Number(value) > 0);
        const chartLabels = hasData ? labels : ['No sales data'];
        const chartData = hasData ? data : [1];
        const chartColors = hasData ? colors : ['#dbe5f1'];

        pieChart = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: chartLabels,
                datasets: [{
                    data: chartData,
                    backgroundColor: chartColors,
                    borderWidth: 0,
                    hoverOffset: 10
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: true,
                plugins: {
                    legend: {
                        display: false // We use custom legend
                    },
                    tooltip: {
                        enabled: hasData,
                        backgroundColor: 'rgba(0, 0, 0, 0.8)',
                        padding: 12,
                        titleFont: {
                            size: 14,
                            weight: 'bold'
                        },
                        bodyFont: {
                            size: 13
                        },
                        callbacks: {
                            label: function (context) {
                                const label = context.label || '';
                                const value = context.parsed || 0;
                                const total = context.dataset.data.reduce((a, b) => a + b, 0);
                                const percentage = total > 0 ? ((value / total) * 100).toFixed(2) : '0.00';
                                return `${label}: MYR ${value.toLocaleString('en-MY', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} (${percentage}%)`;
                            }
                        }
                    }
                },
                cutout: '65%'
            }
        });
    }

    function createBarChart(canvasId, labels, data, colors) {
        // Destroy existing chart if it exists
        if (barChart) {
            barChart.destroy();
        }

        const ctx = document.getElementById(canvasId);
        if (!ctx) {
            console.error(`Canvas element with id '${canvasId}' not found`);
            return;
        }

        barChart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    data: data,
                    backgroundColor: colors,
                    borderRadius: 8,
                    borderSkipped: false
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: true,
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        backgroundColor: 'rgba(0, 0, 0, 0.8)',
                        padding: 12,
                        titleFont: {
                            size: 14,
                            weight: 'bold'
                        },
                        bodyFont: {
                            size: 13
                        },
                        callbacks: {
                            label: function (context) {
                                const value = context.parsed.y || 0;
                                return `MYR ${value.toLocaleString('en-MY', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            callback: function (value) {
                                return 'MYR ' + value.toLocaleString('en-MY');
                            },
                            font: {
                                size: 12
                            },
                            color: '#6b7280'
                        },
                        grid: {
                            color: 'rgba(0, 0, 0, 0.05)',
                            drawBorder: false
                        }
                    },
                    x: {
                        ticks: {
                            font: {
                                size: 12,
                                weight: '600'
                            },
                            color: '#374151'
                        },
                        grid: {
                            display: false
                        }
                    }
                }
            }
        });
    }

    function destroyCharts() {
        if (pieChart) {
            pieChart.destroy();
            pieChart = null;
        }
        if (barChart) {
            barChart.destroy();
            barChart = null;
        }
    }

    return {
        createPieChart: createPieChart,
        createBarChart: createBarChart,
        destroyCharts: destroyCharts
    };
})();