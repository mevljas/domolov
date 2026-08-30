window.domolov = {
  renderPriceChart: function (canvasId, labels, values) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    const w = canvas.width;
    const h = canvas.height;
    ctx.clearRect(0, 0, w, h);
    if (!values || values.length === 0) return;
    const min = Math.min(...values);
    const max = Math.max(...values);
    const pad = 24;
    const span = Math.max(max - min, 1);
    ctx.strokeStyle = '#c45c26';
    ctx.lineWidth = 2;
    ctx.beginPath();
    values.forEach((v, i) => {
      const x = pad + (i * (w - pad * 2)) / Math.max(values.length - 1, 1);
      const y = h - pad - ((v - min) / span) * (h - pad * 2);
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    });
    ctx.stroke();
    ctx.fillStyle = '#333';
    ctx.font = '12px sans-serif';
    ctx.fillText(String(max), 4, pad);
    ctx.fillText(String(min), 4, h - 8);
  }
};
