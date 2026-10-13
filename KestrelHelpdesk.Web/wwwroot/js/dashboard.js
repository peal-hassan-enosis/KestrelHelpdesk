import { Chart, registerables } from "https://cdn.jsdelivr.net/npm/chart.js@4.5.1/+esm";

Chart.register(...registerables);

let chart = null;

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
      if (Number(value) > 0) ctx.fillText(String(value), bar.x, bar.y - 8);
    });

    ctx.restore();
  }
};

export function createStatusChart(canvasId, labels, values) {
  const canvas = document.getElementById(canvasId);

  if (!canvas) {
    destroyChart();
    return;
  }

  // Reuse the existing chart when the DOM canvas is still the same. Blazor can
  // render the page again without replacing the canvas, so avoid rebuilding it
  // unnecessarily while still recovering if navigation replaced the element.
  if (chart && chart.canvas === canvas) {
    const currentLabels = chart.data.labels ?? [];
    const currentValues = chart.data.datasets?.[0]?.data ?? [];
    const dataChanged =
      labels.length !== currentLabels.length ||
      labels.some((label, index) => label !== currentLabels[index]) ||
      values.length !== currentValues.length ||
      values.some((value, index) => value !== currentValues[index]);

    if (dataChanged) {
      chart.data.labels = labels;
      chart.data.datasets[0].data = values;
      chart.update("none");
    }

    return;
  }

  chart?.destroy();
  chart = null;

  // Also clean up any Chart.js instance left on this canvas by a previous
  // component/module instance.
  Chart.getChart(canvas)?.destroy();

  chart = new Chart(canvas, {
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
}

export function destroyChart() {
  if (chart) {
    chart.destroy();
    chart = null;
  }

  const canvas = document.getElementById("ticketStatusChart");
  const existingChart = canvas ? Chart.getChart(canvas) : undefined;
  existingChart?.destroy();
}
