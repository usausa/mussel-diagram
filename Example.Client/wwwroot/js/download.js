export async function download(name, type, stream) {
    const buffer = await stream.arrayBuffer();
    const url = URL.createObjectURL(new Blob([buffer], { type }));
    const link = document.createElement('a');
    link.href = url;
    link.download = name;
    document.body.append(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}
