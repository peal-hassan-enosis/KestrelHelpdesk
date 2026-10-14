import { Chart, registerables } from "https://cdn.jsdelivr.net/npm/chart.js@4.5.1/+esm";

Chart.register(...registerables);

// Key charts by component-instance canvas ID. Blazor can remove/recreate a route's DOM
// while the JavaScript module remains cached in the browser.
const charts = new Map();
const pendingFrames = new Map();

const countLabelsPlugin = {
  id: "ticketCountLabels",
  afterDatasetsDraw(instance) {
    const { ctx } = instance;
    const dataset = instance.data.datasets[0];
    const meta = instance.getDatasetMeta(0);
    ctx.save();
    ctx.font = "600 12px Inter, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillStyle = "#52627b";

    meta.data.forEach((bar, index) => {
      const value = dataset.data[index];
      if (Number(value) > 0) {
        ctx.fillText(String(value), bar.x, bar.y - 8);
      }
    });

    ctx.restore();
  }
};

export function createStatusChart(canvasId, labels, values) {
  // Wait for the browser to finish the current navigation/layout pass. This avoids
  // Chart.js measuring a newly re-created Blazor canvas before its container has size.
  const pendingFrame = pendingFrames.get(canvasId);
  if (pendingFrame !== undefined) {
    cancelAnimationFrame(pendingFrame);
  }

  let attempts = 0;

  const initializeWhenReady = () => {
    const canvas = document.getElementById(canvasId);
    if (!canvas || !canvas.isConnected) {
      pendingFrames.delete(canvasId);
      return;
    }

    const bounds = canvas.parentElement?.getBoundingClientRect();
    if ((!bounds || bounds.width <= 0 || bounds.height <= 0) && attempts < 12) {
      attempts += 1;
      pendingFrames.set(canvasId, requestAnimationFrame(initializeWhenReady));
      return;
    }

    pendingFrames.delete(canvasId);

    const existingChart = charts.get(canvasId);
    if (existingChart && existingChart.canvas === canvas) {
      existingChart.data.labels = labels;
      existingChart.data.datasets[0].data = values;
      existingChart.resize();
      existingChart.update("none");
      return;
    }

    // The canvas was replaced during navigation; dispose only its previous instance.
    if (existingChart) {
      existingChart.destroy();
      charts.delete(canvasId);
    }

    Chart.getChart(canvas)?.destroy();

    const instance = new Chart(canvas, {
      type: "bar",
      plugins: [countLabelsPlugin],
      data: {
        labels,
        datasets: [{
          label: "Tickets",
          data: values,
          backgroundColor: ["#e8ae36", "#4774e8", "#26a47d"],
          borderColor: ["#bd841b", "#315bc8", "#16835f"],
          borderWidth: 1,
          borderRadius: 8,
          borderSkipped: false,
          maxBarThickness: 76
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: 300 },
        interaction: { intersect: false, mode: "index" },
        plugins: {
          legend: { display: false },
          tooltip: {
            backgroundColor: "#142748",
            padding: 12,
            titleFont: { family: "Inter, system-ui, sans-serif", weight: "600" },
            bodyFont: { family: "Inter, system-ui, sans-serif" },
            displayColors: false,
            callbacks: { label: (context) => `${context.parsed.y} ticket(s)` }
          }
        },
        scales: {
          x: {
            grid: { display: false },
            border: { display: false },
            ticks: {
              color: "#65758d",
              font: { family: "Inter, system-ui, sans-serif", size: 11, weight: "600" }
            }
          },
          y: {
            beginAtZero: true,
            suggestedMax: Math.max(1, ...values),
            ticks: {
              precision: 0,
              stepSize: 1,
              color: "#8190a5",
              padding: 10,
              font: { family: "Inter, system-ui, sans-serif", size: 10 }
            },
            grid: { color: "#e9eef6", drawTicks: false },
            border: { display: false }
          }
        }
      }
    });

    charts.set(canvasId, instance);
  };

  pendingFrames.set(canvasId, requestAnimationFrame(initializeWhenReady));
}

export function destroyChart(canvasId) {
  const pendingFrame = pendingFrames.get(canvasId);
  if (pendingFrame !== undefined) {
    cancelAnimationFrame(pendingFrame);
    pendingFrames.delete(canvasId);
  }

  const instance = charts.get(canvasId);
  if (instance) {
    instance.destroy();
    charts.delete(canvasId);
  }

  const canvas = document.getElementById(canvasId);
  if (canvas) {
    Chart.getChart(canvas)?.destroy();
  }
}
