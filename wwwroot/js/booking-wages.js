(() => {
    const normalize = value => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();
    const money = value => new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 }).format(value) + ' đ';
    document.querySelectorAll('.wage-editor').forEach(editor => {
        if (editor.wageEditor) return;
        const rows = [...editor.querySelectorAll('.wage-person')];
        const search = editor.querySelector('[data-wage-search]');
        const department = editor.querySelector('[data-wage-department]');
        const selected = editor.querySelector('[data-wage-selected]');
        const form = editor.closest('form');
        function update() {
            let totalCents = 0, count = 0, visible = 0;
            rows.forEach(row => {
                const choice = row.querySelector('[data-wage-choice]');
                const amount = row.querySelector('[data-wage-amount]');
                amount.disabled = !choice.checked;
                amount.setCustomValidity('');
                if (choice.checked) { totalCents += Math.round((Number(amount.value) || 0) * 100); count++; }
                row.querySelector('[data-wage-formatted]').textContent = choice.checked ? money(Number(amount.value) || 0) : 'Chưa chọn';
                row.hidden = !normalize(row.dataset.name).includes(normalize(search.value.trim())) || (department.value && department.value !== row.dataset.department) || (selected.checked && !choice.checked);
                if (!row.hidden) visible++;
            });
            const poolCents = Math.round((Number(editor.dataset.pool) || 0) * 100);
            editor.querySelector('[data-wage-pool]').textContent = money(poolCents / 100);
            editor.querySelector('[data-wage-total]').textContent = money(totalCents / 100);
            editor.querySelector('[data-wage-remaining]').textContent = money((poolCents - totalCents) / 100);
            editor.querySelector('.wage-count').textContent = `Đã chọn ${count} người · Hiển thị ${visible}/${rows.length} nhân viên`;
            editor.querySelector('[data-wage-empty]').hidden = visible > 0;
            const error = editor.querySelector('.wage-error');
            error.hidden = totalCents <= poolCents;
            error.textContent = totalCents > poolCents ? `Vượt quỹ ${money((totalCents - poolCents) / 100)}. Giảm số tiền chia trước khi lưu.` : '';
            return totalCents <= poolCents;
        }
        editor.addEventListener('input', update);
        editor.addEventListener('change', update);
        editor.addEventListener('invalid', () => {
            search.value = ''; department.value = ''; selected.checked = false; update();
        }, true);
        form.addEventListener('submit', event => {
            if (editor.closest('fieldset')?.disabled) return;
            if (!update()) { event.preventDefault(); editor.querySelector('.wage-error').scrollIntoView({ block: 'center' }); }
        });
        editor.wageEditor = {
            setPool(value) { editor.dataset.pool = value; update(); },
            reset() {
                search.value = ''; department.value = ''; selected.checked = false;
                rows.forEach(row => { row.querySelector('[data-wage-choice]').checked = false; row.querySelector('[data-wage-amount]').value = '0'; });
                update();
            }
        };
        update();
    });
})();
