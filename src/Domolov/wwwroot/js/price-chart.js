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

    // Subtle grid
    ctx.strokeStyle = '#c9d2cb';
    ctx.lineWidth = 1;
    for (let i = 0; i < 3; i++) {
      const y = pad + ((h - pad * 2) * i) / 2;
      ctx.beginPath();
      ctx.moveTo(pad, y);
      ctx.lineTo(w - pad, y);
      ctx.stroke();
    }

    ctx.strokeStyle = '#1f4d3a';
    ctx.lineWidth = 2.5;
    ctx.lineJoin = 'round';
    ctx.beginPath();
    values.forEach((v, i) => {
      const x = pad + (i * (w - pad * 2)) / Math.max(values.length - 1, 1);
      const y = h - pad - ((v - min) / span) * (h - pad * 2);
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    });
    ctx.stroke();

    ctx.fillStyle = '#1f4d3a';
    values.forEach((v, i) => {
      const x = pad + (i * (w - pad * 2)) / Math.max(values.length - 1, 1);
      const y = h - pad - ((v - min) / span) * (h - pad * 2);
      ctx.beginPath();
      ctx.arc(x, y, 3.5, 0, Math.PI * 2);
      ctx.fill();
    });

    ctx.fillStyle = '#5a6360';
    ctx.font = '12px "Source Sans 3", sans-serif';
    ctx.fillText(String(max), 4, pad);
    ctx.fillText(String(min), 4, h - 8);
  }
};
