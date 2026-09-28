export async function drawAtlas(canvasId, pngBase64, width, height, sprites, selectedIndex) {
    const canvas = typeof canvasId === "string" ? document.getElementById(canvasId) : canvasId;
    if (!(canvas instanceof HTMLCanvasElement)) return;
    const context = canvas.getContext("2d");
    if (!context) return;

    canvas.width = width;
    canvas.height = height;
    canvas.style.aspectRatio = `${width} / ${height}`;

    const tile = Math.max(8, Math.floor(Math.min(width, height) / 48));
    for (let y = 0; y < height; y += tile) {
        for (let x = 0; x < width; x += tile) {
            context.fillStyle = ((Math.floor(x / tile) + Math.floor(y / tile)) % 2) ? "#30343a" : "#24282d";
            context.fillRect(x, y, tile, tile);
        }
    }

    const image = new Image();
    image.src = `data:image/png;base64,${pngBase64}`;
    await image.decode();
    context.drawImage(image, 0, 0, width, height);

    const scale = Math.max(1, Math.min(width, height) / 512);
    sprites.forEach((sprite, index) => {
        const x = sprite.x ?? sprite.X;
        const y = sprite.y ?? sprite.Y;
        const spriteWidth = sprite.width ?? sprite.Width;
        const spriteHeight = sprite.height ?? sprite.Height;
        const name = sprite.name ?? sprite.Name;
        const top = y;
        const selected = index === selectedIndex;
        context.save();
        context.strokeStyle = selected ? "#ffffff" : "#50c7ff";
        context.lineWidth = selected ? 3 * scale : 2 * scale;
        context.fillStyle = selected ? "rgba(20, 112, 220, .22)" : "rgba(20, 112, 220, .08)";
        context.fillRect(x, top, spriteWidth, spriteHeight);
        context.strokeRect(x, top, spriteWidth, spriteHeight);

        const fontSize = Math.max(8, Math.min(16, Math.min(width, height) / 64));
        context.font = `600 ${fontSize}px sans-serif`;
        context.textBaseline = "top";
        const label = name;
        const labelWidth = Math.min(spriteWidth, context.measureText(label).width + 8);
        const labelHeight = fontSize + 8;
        context.fillStyle = "rgba(0, 0, 0, .78)";
        context.fillRect(x, top, labelWidth, Math.min(labelHeight, spriteHeight));
        context.fillStyle = "#ffffff";
        context.fillText(label, x + 4, top + 4, Math.max(0, labelWidth - 8));
        context.restore();
    });
}
