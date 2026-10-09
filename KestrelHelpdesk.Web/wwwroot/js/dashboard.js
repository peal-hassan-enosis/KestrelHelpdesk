import { Chart, registerables } from "https://cdn.jsdelivr.net/npm/chart.js@4.5.1/+esm";

Chart.register(...registerables);

let chart = null;

export function createStatusChart(canvasId, labels, values) {
  const canvas = document.getElementById(canvasId);

  if (!canvas) {
    return;
  }

  chart?.destroy();

  chart = new Chart(canvas, {
    type: "bar",
    data: {
      labels: labels,
      datasets: [
        {
          label: "Tickets",
          data: values,
          backgroundColor: [
            "#ffc107", // Open
            "#0d6efd", // In Progress
            "#198754"  // Resolved
          ],
          borderColor: [
            "#ffc107",
            "#0d6efd",
            "#198754"
          ],
          borderWidth: 1
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        y: {
          beginAtZero: true,
          ticks: {
            precision: 0
          }
        }
      }
    }
  });
}

export function destroyChart() {
  if (chart) {
    chart.destroy();
    chart = null;
  }
}