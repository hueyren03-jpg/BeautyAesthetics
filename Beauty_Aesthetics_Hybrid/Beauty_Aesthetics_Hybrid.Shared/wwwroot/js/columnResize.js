window.initColumnResize = () => {
    const tables = document.querySelectorAll('.resizable-table');
    if (!tables.length) return;

    tables.forEach(table => {
        const ths = table.querySelectorAll('th');

        ths.forEach((th, index) => {
            const handle = th.querySelector('.resize-handle');
            if (!handle || handle.dataset.resizeBound === 'true') return;

            handle.dataset.resizeBound = 'true';

            let startX = 0;
            let startWidth = 0;

            const onMouseMove = e => {
                const newWidth = Math.max(48, startWidth + (e.pageX - startX));
                th.style.width = newWidth + 'px';

                table.querySelectorAll(`td:nth-child(${index + 1})`)
                    .forEach(td => td.style.width = newWidth + 'px');
            };

            const onMouseUp = () => {
                document.removeEventListener('mousemove', onMouseMove);
                document.removeEventListener('mouseup', onMouseUp);
            };

            handle.addEventListener('mousedown', e => {
                e.preventDefault();
                startX = e.pageX;
                startWidth = th.offsetWidth;

                document.addEventListener('mousemove', onMouseMove);
                document.addEventListener('mouseup', onMouseUp);
            });
        });
    });
};