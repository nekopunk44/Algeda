(function (global) {
    function realEstateCreateCriteriaInit(config) {
        if (!config || !Array.isArray(config.definitions)) {
            return;
        }

        const definitions = normalizeDefinitions(config.definitions);
        const definitionMap = new Map(definitions.map((x) => [String(x.id), x]));
        const selectedCriteria = normalizeSelectedCriteria(config.selectedCriteria)
            .filter((item) => definitionMap.has(item.criterionDefinitionId));
        const pickerPageSize = 20;

        const searchInput = document.getElementById(config.criterionSearchInputId || "");
        const categoryFilter = document.getElementById(config.criterionCategoryFilterId || "");
        const pickerList = document.getElementById(config.criterionPickerListId || "");
        const pickerPagination = document.getElementById(config.criterionPickerPaginationId || "");
        const pickerModalElement = document.getElementById(config.criterionPickerModalId || "");
        const openPickerButton = document.getElementById(config.criterionOpenPickerButtonId || "");
        const selectedNameInput = document.getElementById(config.criterionSelectedNameId || "");
        const form = document.getElementById(config.formId || "");

        const definitionSelect = document.getElementById(config.criterionDefinitionSelectId || "");
        const editor = document.getElementById(config.criterionEditorId || "");
        const addButton = document.getElementById(config.addCriterionButtonId || "");
        const body = document.getElementById(config.selectedCriteriaBodyId || "");
        const emptyRow = document.getElementById(config.selectedCriteriaEmptyRowId || "");
        const jsonInput = document.getElementById(config.criteriaJsonInputId || "");

        const pickerModal = pickerModalElement && global.bootstrap
            ? new global.bootstrap.Modal(pickerModalElement)
            : null;

        if (!(definitionSelect instanceof HTMLSelectElement)
            || !(editor instanceof HTMLElement)
            || !(body instanceof HTMLElement)
            || !(jsonInput instanceof HTMLInputElement)) {
            return;
        }

        let pickerPage = 1;

        definitionSelect.addEventListener("change", () => {
            renderEditor(definitionSelect.value, null);
            syncSelectedName();
        });

        openPickerButton?.addEventListener("click", () => {
            pickerPage = 1;
            if (searchInput instanceof HTMLInputElement) {
                searchInput.value = "";
            }
            if (categoryFilter instanceof HTMLSelectElement) {
                categoryFilter.value = "";
            }
            renderPicker();
            pickerModal?.show();
        });

        searchInput?.addEventListener("input", () => {
            pickerPage = 1;
            renderPicker();
        });

        categoryFilter?.addEventListener("change", () => {
            pickerPage = 1;
            renderPicker();
        });

        pickerPagination?.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof HTMLElement)) {
                return;
            }

            const pageButton = target.closest(".js-picker-page");
            if (!(pageButton instanceof HTMLElement)) {
                return;
            }

            const page = Number(pageButton.dataset.page || "1");
            if (!Number.isFinite(page) || page < 1) {
                return;
            }

            pickerPage = page;
            renderPicker();
        });

        pickerList?.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof HTMLElement)) {
                return;
            }

            const button = target.closest(".js-pick-criterion");
            if (!(button instanceof HTMLElement)) {
                return;
            }

            const definitionId = String(button.dataset.definitionId || "").trim();
            if (!definitionMap.has(definitionId)) {
                return;
            }

            definitionSelect.value = definitionId;
            renderEditor(definitionId, null);
            syncSelectedName();
            pickerModal?.hide();
        });

        addButton?.addEventListener("click", () => {
            const definitionId = definitionSelect.value;
            const definition = definitionMap.get(String(definitionId));
            if (!definition) {
                showEditorMessage("Сначала выберите критерий.", true);
                return;
            }

            const payload = extractValueFromEditor(definition, false);
            if (!payload) {
                return;
            }

            if (mergeCriterionPayload(payload)) {
                showEditorMessage("Значение критерия обновлено.", false);
            } else {
                showEditorMessage("Критерий добавлен.", false);
            }

            syncJson();
            renderSelectedTable();
            renderPicker();
        });

        if (form instanceof HTMLFormElement) {
            form.addEventListener("submit", () => {
                const definitionId = definitionSelect.value;
                const definition = definitionMap.get(String(definitionId));
                if (definition) {
                    const draftPayload = extractValueFromEditor(definition, true);
                    if (draftPayload) {
                        mergeCriterionPayload(draftPayload);
                    }
                }

                syncJson();
            });
        }

        initCategoryFilter();
        renderEditor("", null);
        renderSelectedTable();
        syncJson();
        syncSelectedName();

        function initCategoryFilter() {
            if (!(categoryFilter instanceof HTMLSelectElement)) {
                return;
            }

            const categories = [...new Set(
                definitions
                    .map((x) => String(x.category || "").trim())
                    .filter(Boolean)
            )].sort((a, b) => a.localeCompare(b));

            categoryFilter.innerHTML = '<option value="">Не важно</option>';
            categories.forEach((category) => {
                const option = document.createElement("option");
                option.value = category;
                option.textContent = category;
                categoryFilter.appendChild(option);
            });
        }

        function renderPicker() {
            if (!(pickerList instanceof HTMLElement)) {
                return;
            }

            const filtered = getFilteredDefinitions();
            const totalPages = Math.max(1, Math.ceil(filtered.length / pickerPageSize));
            pickerPage = Math.min(Math.max(pickerPage, 1), totalPages);

            const start = (pickerPage - 1) * pickerPageSize;
            const pageItems = filtered.slice(start, start + pickerPageSize);

            pickerList.innerHTML = pageItems.map((definition) => {
                const alreadySelected = selectedCriteria.some((x) => x.criterionDefinitionId === definition.id);
                const badge = alreadySelected ? "Обновить" : "Добавить";
                return `
                    <button type="button" class="list-group-item list-group-item-action d-flex justify-content-between align-items-start js-pick-criterion" data-definition-id="${escapeHtml(definition.id)}">
                        <span class="me-3 text-start">
                            <span class="fw-semibold d-block">${escapeHtml(definition.displayName)}</span>
                            <span class="small text-muted d-block">${escapeHtml(definition.description || "—")}</span>
                            <span class="small d-block mt-1">Варианты: ${escapeHtml(buildOptionsText(definition))}</span>
                        </span>
                        <span class="badge text-bg-light border mt-1">${badge}</span>
                    </button>
                `;
            }).join("") || '<div class="small text-muted">Ничего не найдено.</div>';

            renderPickerPagination(totalPages);
        }

        function renderPickerPagination(totalPages) {
            if (!(pickerPagination instanceof HTMLElement)) {
                return;
            }

            if (totalPages <= 1) {
                pickerPagination.innerHTML = "";
                return;
            }

            pickerPagination.innerHTML = Array.from({ length: totalPages }, (_, index) => {
                const page = index + 1;
                return `
                    <li class="page-item ${page === pickerPage ? "active" : ""}">
                        <button type="button" class="page-link js-picker-page" data-page="${page}">${page}</button>
                    </li>
                `;
            }).join("");
        }

        function getFilteredDefinitions() {
            const query = String(searchInput?.value || "").trim().toLowerCase();
            const selectedCategory = String(categoryFilter?.value || "").trim().toLowerCase();

            return definitions.filter((definition) => {
                if (selectedCategory && String(definition.category || "").toLowerCase() !== selectedCategory) {
                    return false;
                }

                if (!query) {
                    return true;
                }

                const haystack = `${definition.code} ${definition.displayName} ${definition.category}`.toLowerCase();
                return haystack.includes(query);
            });
        }

        function renderEditor(definitionId, existingItem) {
            const definition = definitionMap.get(String(definitionId));
            if (!definition) {
                showEditorMessage("После выбора критерия появится поле ввода значения.");
                return;
            }

            const valueType = String(definition.valueType || "Undefined");
            const valueTypeLabel = typeLabel(valueType);
            const existingValue = existingItem?.value || "";
            const existingValues = Array.isArray(existingItem?.values) ? existingItem.values : [];

            if (valueType === "Boolean") {
                const checked = normalizeBoolean(existingValue);
                editor.innerHTML = `
                    <div class="fw-semibold mb-2">${escapeHtml(definition.displayName)} (${escapeHtml(valueTypeLabel)})</div>
                    <div class="form-check form-switch">
                        <input class="form-check-input" type="checkbox" id="criterionValueBoolean" ${checked ? "checked" : ""}>
                        <label class="form-check-label" for="criterionValueBoolean">Да / Нет</label>
                    </div>
                `;
                return;
            }

            if (valueType === "Number") {
                editor.innerHTML = `
                    <div class="fw-semibold mb-2">${escapeHtml(definition.displayName)} (${escapeHtml(valueTypeLabel)})</div>
                    <input id="criterionValueNumber" type="number" step="0.01" class="form-control" value="${escapeHtml(existingValue)}" />
                `;
                return;
            }

            if (valueType === "Text") {
                editor.innerHTML = `
                    <div class="fw-semibold mb-2">${escapeHtml(definition.displayName)} (${escapeHtml(valueTypeLabel)})</div>
                    <input id="criterionValueText" type="text" class="form-control" value="${escapeHtml(existingValue)}" />
                `;
                return;
            }

            if (valueType === "SingleSelect") {
                const options = (definition.options || []).map((opt) => {
                    const selected = String(opt.value || "") === String(existingValue || "") ? "selected" : "";
                    return `<option value="${escapeHtml(String(opt.value || ""))}" ${selected}>${escapeHtml(String(opt.label || opt.value || ""))}</option>`;
                }).join("");

                editor.innerHTML = `
                    <div class="fw-semibold mb-2">${escapeHtml(definition.displayName)} (${escapeHtml(valueTypeLabel)})</div>
                    <select id="criterionValueSingleSelect" class="form-select">
                        <option value="">-- выберите --</option>
                        ${options}
                    </select>
                `;
                return;
            }

            if (valueType === "MultiSelect") {
                const selectedSet = new Set(existingValues.map((x) => String(x)));
                const options = (definition.options || []).map((opt, index) => {
                    const value = String(opt.value || "");
                    const id = `multi_option_${index}`;
                    const checked = selectedSet.has(value) ? "checked" : "";
                    return `
                        <div class="form-check">
                            <input class="form-check-input" type="checkbox" id="${id}" value="${escapeHtml(value)}" ${checked}>
                            <label class="form-check-label" for="${id}">${escapeHtml(String(opt.label || value))}</label>
                        </div>
                    `;
                }).join("");

                editor.innerHTML = `
                    <div class="fw-semibold mb-2">${escapeHtml(definition.displayName)} (${escapeHtml(valueTypeLabel)})</div>
                    <div class="d-grid gap-1" id="criterionValueMultiSelectWrap">
                        ${options}
                    </div>
                `;
                return;
            }

            showEditorMessage("Неподдерживаемый тип критерия.", true);
        }

        function extractValueFromEditor(definition, silent) {
            const definitionId = String(definition.id);
            const valueType = String(definition.valueType || "Undefined");
            const showValidationError = (message) => {
                if (!silent) {
                    showEditorMessage(message, true);
                }
            };

            if (valueType === "Boolean") {
                const control = document.getElementById("criterionValueBoolean");
                if (!(control instanceof HTMLInputElement)) {
                    return null;
                }

                return {
                    criterionDefinitionId: definitionId,
                    value: control.checked ? "true" : "false",
                    values: null
                };
            }

            if (valueType === "Number") {
                const control = document.getElementById("criterionValueNumber");
                if (!(control instanceof HTMLInputElement) || !control.value.trim()) {
                    showValidationError("Введите числовое значение.");
                    return null;
                }

                return {
                    criterionDefinitionId: definitionId,
                    value: control.value.trim(),
                    values: null
                };
            }

            if (valueType === "Text") {
                const control = document.getElementById("criterionValueText");
                if (!(control instanceof HTMLInputElement) || !control.value.trim()) {
                    showValidationError("Введите текстовое значение.");
                    return null;
                }

                return {
                    criterionDefinitionId: definitionId,
                    value: control.value.trim(),
                    values: null
                };
            }

            if (valueType === "SingleSelect") {
                const control = document.getElementById("criterionValueSingleSelect");
                if (!(control instanceof HTMLSelectElement) || !control.value.trim()) {
                    showValidationError("Выберите значение из списка.");
                    return null;
                }

                return {
                    criterionDefinitionId: definitionId,
                    value: control.value.trim(),
                    values: null
                };
            }

            if (valueType === "MultiSelect") {
                const selected = Array.from(editor.querySelectorAll("#criterionValueMultiSelectWrap input[type='checkbox']:checked"))
                    .map((x) => String(x.value || "").trim())
                    .filter(Boolean);

                if (selected.length === 0) {
                    showValidationError("Выберите хотя бы одно значение.");
                    return null;
                }

                return {
                    criterionDefinitionId: definitionId,
                    value: null,
                    values: selected
                };
            }

            showValidationError("Неподдерживаемый тип критерия.");
            return null;
        }

        function mergeCriterionPayload(payload) {
            const existingIndex = selectedCriteria.findIndex((x) => x.criterionDefinitionId === payload.criterionDefinitionId);
            if (existingIndex >= 0) {
                selectedCriteria[existingIndex] = payload;
                return true;
            }

            selectedCriteria.push(payload);
            return false;
        }

        function renderSelectedTable() {
            body.querySelectorAll("tr[data-criterion-id]").forEach((row) => row.remove());

            if (selectedCriteria.length === 0) {
                if (emptyRow) {
                    emptyRow.style.display = "";
                }
                return;
            }

            if (emptyRow) {
                emptyRow.style.display = "none";
            }

            selectedCriteria.forEach((item) => {
                const definition = definitionMap.get(String(item.criterionDefinitionId));
                const row = document.createElement("tr");
                row.dataset.criterionId = String(item.criterionDefinitionId);

                const displayValue = formatValueForDisplay(definition, item);
                const typeText = typeLabel(definition?.valueType);
                const criterionName = definition ? definition.displayName : String(item.criterionDefinitionId);

                row.innerHTML = `
                    <td>${escapeHtml(criterionName)}</td>
                    <td>${escapeHtml(typeText)}</td>
                    <td>${escapeHtml(displayValue)}</td>
                    <td>
                        <div class="d-flex gap-2">
                            <button type="button" class="btn btn-outline-primary btn-sm js-edit-item">Изменить</button>
                            <button type="button" class="btn btn-outline-danger btn-sm js-delete-item">Удалить</button>
                        </div>
                    </td>
                `;

                row.querySelector(".js-edit-item")?.addEventListener("click", () => {
                    definitionSelect.value = String(item.criterionDefinitionId);
                    renderEditor(item.criterionDefinitionId, item);
                    syncSelectedName();
                    editor.scrollIntoView({ behavior: "smooth", block: "center" });
                });

                row.querySelector(".js-delete-item")?.addEventListener("click", () => {
                    const index = selectedCriteria.findIndex((x) => x.criterionDefinitionId === item.criterionDefinitionId);
                    if (index >= 0) {
                        selectedCriteria.splice(index, 1);
                        syncJson();
                        renderSelectedTable();
                        renderPicker();
                    }
                });

                body.appendChild(row);
            });
        }

        function formatValueForDisplay(definition, item) {
            const valueType = String(definition?.valueType || "");

            if (valueType === "Boolean") {
                return normalizeBoolean(item.value) ? "Да" : "Нет";
            }

            if (valueType === "SingleSelect") {
                const selected = String(item.value || "");
                const option = (definition?.options || []).find((x) => String(x.value || "") === selected);
                return option ? String(option.label || option.value || selected) : selected;
            }

            if (valueType === "MultiSelect") {
                const values = Array.isArray(item.values) ? item.values : [];
                return values.map((value) => {
                    const option = (definition?.options || []).find((x) => String(x.value || "") === String(value));
                    return option ? String(option.label || option.value || value) : String(value);
                }).join(", ");
            }

            return String(item.value || "");
        }

        function syncJson() {
            jsonInput.value = JSON.stringify(selectedCriteria);
        }

        function syncSelectedName() {
            if (!(selectedNameInput instanceof HTMLInputElement)) {
                return;
            }

            const selected = definitionMap.get(String(definitionSelect.value || ""));
            selectedNameInput.value = selected ? selected.displayName : "Критерий не выбран";
        }

        function showEditorMessage(message, isError) {
            editor.innerHTML = `<span class="small ${isError ? "text-danger" : "text-muted"}">${escapeHtml(message)}</span>`;
        }
    }

    function normalizeDefinitions(definitions) {
        return definitions
            .map((item) => ({
                id: String(item?.id || ""),
                code: String(item?.code || ""),
                displayName: String(item?.displayName || "").trim(),
                category: String(item?.category || "").trim(),
                description: String(item?.description || "").trim(),
                valueType: String(item?.valueType || "Undefined"),
                options: Array.isArray(item?.options)
                    ? item.options
                        .map((option) => ({
                            value: String(option?.value || ""),
                            label: String(option?.label || option?.value || "")
                        }))
                    : []
            }))
            .filter((item) => item.id.length > 0)
            .sort((left, right) => left.displayName.localeCompare(right.displayName));
    }

    function normalizeSelectedCriteria(criteria) {
        if (!Array.isArray(criteria) || criteria.length === 0) {
            return [];
        }

        const map = new Map();

        criteria.forEach((item) => {
            const criterionDefinitionId = String(item?.criterionDefinitionId || "").trim();
            if (!criterionDefinitionId) {
                return;
            }

            const value = item?.value == null ? null : String(item.value).trim();
            const values = Array.isArray(item?.values)
                ? item.values
                    .map((x) => String(x || "").trim())
                    .filter(Boolean)
                : [];

            if (!value && values.length === 0) {
                return;
            }

            map.set(criterionDefinitionId, {
                criterionDefinitionId,
                value: value || null,
                values: values.length > 0 ? values : null
            });
        });

        return Array.from(map.values());
    }

    function buildOptionsText(definition) {
        const labels = (definition?.options || [])
            .map((option) => String(option.label || option.value || "").trim())
            .filter(Boolean);

        const unique = [...new Set(labels)];
        return unique.length > 0 ? unique.join(", ") : "—";
    }

    function typeLabel(valueType) {
        switch (String(valueType || "")) {
            case "Boolean":
                return "Да/Нет";
            case "Number":
                return "Число";
            case "Text":
                return "Текст";
            case "SingleSelect":
                return "Один вариант";
            case "MultiSelect":
                return "Несколько вариантов";
            default:
                return "Не указан";
        }
    }

    function normalizeBoolean(value) {
        const normalized = String(value || "").trim().toLowerCase();
        return normalized === "true" || normalized === "1" || normalized === "yes" || normalized === "да";
    }

    function escapeHtml(value) {
        return String(value || "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll("\"", "&quot;")
            .replaceAll("'", "&#39;");
    }

    global.realEstateCreateCriteriaInit = realEstateCreateCriteriaInit;
})(window);
