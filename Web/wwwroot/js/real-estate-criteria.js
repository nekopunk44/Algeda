(function (global) {
    function realEstateCriteriaInit(init) {
        if (!init) {
            return;
        }

        const definitions = normalizeDefinitions(Array.isArray(init.definitions) ? init.definitions : []);
        const definitionsById = new Map(definitions.map((x) => [x.id, x]));

        const definitionSelect = document.getElementById("criterionDefinitionId");
        const typeLabel = document.getElementById("criterionValueTypeLabel");
        const valueContainer = document.getElementById("criterionValueContainer");
        const resetButton = document.getElementById("criterionFormReset");

        if (!(definitionSelect instanceof HTMLSelectElement) || !valueContainer || !typeLabel) {
            return;
        }

        const pickerSearch = document.getElementById(init.criterionSearchInputId || "");
        const pickerCategory = document.getElementById(init.criterionCategoryFilterId || "");
        const pickerList = document.getElementById(init.criterionPickerListId || "");
        const pickerPagination = document.getElementById(init.criterionPickerPaginationId || "");
        const pickerModalElement = document.getElementById(init.criterionPickerModalId || "");
        const openPickerButton = document.getElementById(init.criterionOpenPickerButtonId || "");
        const selectedNameInput = document.getElementById(init.criterionSelectedNameId || "");
        const criteriaJsonInput = document.getElementById(init.criteriaJsonInputId || "");
        const updateForm = document.getElementById(init.updateFormId || "");
        const pickerModal = pickerModalElement && global.bootstrap
            ? new global.bootstrap.Modal(pickerModalElement)
            : null;

        const pickerPageSize = 20;
        let pickerPage = 1;
        const selectedCriteria = normalizeExistingToSubmitList(Array.isArray(init.existing) ? init.existing : []);

        definitionSelect.addEventListener("change", () => {
            renderValueInput(definitionSelect.value, null);
            syncSelectedName();
        });

        resetButton?.addEventListener("click", () => {
            definitionSelect.value = "";
            renderValueInput("", null);
            syncSelectedName();
        });

        document.querySelectorAll(".js-edit-criterion").forEach((button) => {
            button.addEventListener("click", () => {
                if (!(button instanceof HTMLElement)) {
                    return;
                }

                const definitionId = button.dataset.definitionId || "";
                const rawValue = button.dataset.rawValue || "";

                definitionSelect.value = definitionId;
                renderValueInput(definitionId, rawValue);
                syncSelectedName();
                valueContainer.scrollIntoView({ behavior: "smooth", block: "center" });
            });
        });

        openPickerButton?.addEventListener("click", () => {
            pickerPage = 1;
            if (pickerSearch instanceof HTMLInputElement) {
                pickerSearch.value = "";
            }
            if (pickerCategory instanceof HTMLSelectElement) {
                pickerCategory.value = "";
            }
            renderPicker();
            pickerModal?.show();
        });

        pickerSearch?.addEventListener("input", () => {
            pickerPage = 1;
            renderPicker();
        });

        pickerCategory?.addEventListener("change", () => {
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
            if (!definitionId || !definitionsById.has(definitionId)) {
                return;
            }

            definitionSelect.value = definitionId;
            renderValueInput(definitionId, null);
            syncSelectedName();
            pickerModal?.hide();
        });

        if (updateForm instanceof HTMLFormElement) {
            updateForm.addEventListener("submit", () => {
                mergeDraftCriterionFromEditor();
                syncCriteriaJson();
            });
        }

        initCategoryFilter();
        renderValueInput(definitionSelect.value, null);
        syncSelectedName();
        syncCriteriaJson();

        function renderPicker() {
            if (!(pickerList instanceof HTMLElement)) {
                return;
            }

            const filtered = getFilteredDefinitions();
            const totalPages = Math.max(1, Math.ceil(filtered.length / pickerPageSize));
            pickerPage = Math.min(Math.max(pickerPage, 1), totalPages);

            const start = (pickerPage - 1) * pickerPageSize;
            const pageItems = filtered.slice(start, start + pickerPageSize);

            pickerList.innerHTML = pageItems.map((definition) => `
                <button type="button" class="list-group-item list-group-item-action d-flex justify-content-between align-items-start js-pick-criterion" data-definition-id="${escapeHtml(definition.id)}">
                    <span class="me-3 text-start">
                        <span class="fw-semibold d-block">${escapeHtml(definition.displayName)}</span>
                        <span class="small text-muted d-block">${escapeHtml(definition.description || "—")}</span>
                        <span class="small d-block mt-1">Варианты: ${escapeHtml(buildOptionsText(definition))}</span>
                    </span>
                    <span class="badge text-bg-light border mt-1">Выбрать</span>
                </button>
            `).join("") || '<div class="small text-muted">Ничего не найдено.</div>';

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
            const query = String(pickerSearch?.value || "").trim().toLowerCase();
            const selectedCategory = String(pickerCategory?.value || "").trim().toLowerCase();

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

        function initCategoryFilter() {
            if (!(pickerCategory instanceof HTMLSelectElement)) {
                return;
            }

            const categories = [...new Set(
                definitions
                    .map((x) => String(x.category || "").trim())
                    .filter(Boolean)
            )].sort((a, b) => a.localeCompare(b));

            pickerCategory.innerHTML = '<option value="">Не важно</option>';
            categories.forEach((category) => {
                const option = document.createElement("option");
                option.value = category;
                option.textContent = category;
                pickerCategory.appendChild(option);
            });
        }

        function renderValueInput(definitionId, rawValue) {
            const definition = definitionsById.get(String(definitionId));
            if (!definition) {
                typeLabel.value = "Выберите критерий";
                valueContainer.innerHTML = "<div class=\"text-muted small\">После выбора критерия появится соответствующий ввод значения.</div>";
                return;
            }

            const valueType = String(definition.valueType || "Undefined");
            typeLabel.value = mapValueType(valueType);

            switch (valueType) {
                case "Boolean":
                    renderBoolean(rawValue);
                    break;
                case "Number":
                    renderNumber(rawValue);
                    break;
                case "Text":
                    renderText(rawValue);
                    break;
                case "SingleSelect":
                    renderSingleSelect(definition, rawValue);
                    break;
                case "MultiSelect":
                    renderMultiSelect(definition, rawValue);
                    break;
                default:
                    valueContainer.innerHTML = "<div class=\"alert alert-warning mb-0\">Неподдерживаемый тип критерия.</div>";
                    break;
            }
        }

        function renderBoolean(rawValue) {
            const normalized = String(rawValue || "").trim().toLowerCase();
            const checked = normalized === "true" || normalized === "1" || normalized === "yes" || normalized === "да";

            valueContainer.innerHTML = `
                <input type="hidden" name="value" id="criterionBooleanValue" value="${checked ? "true" : "false"}" />
                <div class="form-check form-switch">
                    <input class="form-check-input" type="checkbox" id="criterionBooleanSwitch" ${checked ? "checked" : ""} />
                    <label class="form-check-label" for="criterionBooleanSwitch">Значение</label>
                </div>
            `;

            const boolSwitch = document.getElementById("criterionBooleanSwitch");
            const hidden = document.getElementById("criterionBooleanValue");
            if (boolSwitch instanceof HTMLInputElement && hidden instanceof HTMLInputElement) {
                boolSwitch.addEventListener("change", () => {
                    hidden.value = boolSwitch.checked ? "true" : "false";
                });
            }
        }

        function renderNumber(rawValue) {
            const value = String(rawValue || "").trim();
            valueContainer.innerHTML = `
                <label class="form-label">Числовое значение</label>
                <input type="number" step="0.01" name="value" class="form-control" value="${escapeHtml(value)}" required />
            `;
        }

        function renderText(rawValue) {
            const value = String(rawValue || "").trim();
            valueContainer.innerHTML = `
                <label class="form-label">Текстовое значение</label>
                <input type="text" name="value" class="form-control" value="${escapeHtml(value)}" required />
            `;
        }

        function renderSingleSelect(definition, rawValue) {
            const options = Array.isArray(definition.options) ? definition.options : [];
            const selectedRaw = normalizeScalar(rawValue);
            const html = options.map((option) => {
                const optionValue = String(option.value || "");
                const selected = optionValue === selectedRaw ? "selected" : "";
                return `<option value="${escapeHtml(optionValue)}" ${selected}>${escapeHtml(option.label || optionValue)}</option>`;
            }).join("");

            valueContainer.innerHTML = `
                <label class="form-label">Выберите значение</label>
                <select name="value" class="form-select" required>
                    <option value="">-- выберите --</option>
                    ${html}
                </select>
            `;
        }

        function renderMultiSelect(definition, rawValue) {
            const options = Array.isArray(definition.options) ? definition.options : [];
            const selectedValues = parseMulti(rawValue);
            const selectedSet = new Set(selectedValues);

            const html = options.map((option, idx) => {
                const optionValue = String(option.value || "");
                const checked = selectedSet.has(optionValue) ? "checked" : "";
                const id = `criterionMulti_${idx}_${optionValue.replace(/[^a-zA-Z0-9_-]/g, "_")}`;
                return `
                    <div class="form-check">
                        <input class="form-check-input" type="checkbox" id="${id}" name="values" value="${escapeHtml(optionValue)}" ${checked} />
                        <label class="form-check-label" for="${id}">${escapeHtml(option.label || optionValue)}</label>
                    </div>
                `;
            }).join("");

            valueContainer.innerHTML = `
                <label class="form-label">Выберите одно или несколько значений</label>
                <div class="d-grid gap-1">${html}</div>
            `;
        }

        function mergeDraftCriterionFromEditor() {
            const definition = definitionsById.get(String(definitionSelect.value || ""));
            if (!definition) {
                return;
            }

            const payload = extractDraftFromEditor(definition);
            if (!payload) {
                return;
            }

            upsertSelectedCriterion(payload);
        }

        function upsertSelectedCriterion(payload) {
            const existingIndex = selectedCriteria.findIndex((x) => x.criterionDefinitionId === payload.criterionDefinitionId);
            if (existingIndex >= 0) {
                selectedCriteria[existingIndex] = payload;
                return;
            }

            selectedCriteria.push(payload);
        }

        function extractDraftFromEditor(definition) {
            const definitionId = String(definition.id || "");
            if (!definitionId) {
                return null;
            }

            const valueType = String(definition.valueType || "Undefined");

            if (valueType === "Boolean") {
                const hidden = valueContainer.querySelector("#criterionBooleanValue");
                if (hidden instanceof HTMLInputElement) {
                    return {
                        criterionDefinitionId: definitionId,
                        value: hidden.value,
                        values: null
                    };
                }

                const boolSwitch = valueContainer.querySelector("#criterionBooleanSwitch");
                if (boolSwitch instanceof HTMLInputElement) {
                    return {
                        criterionDefinitionId: definitionId,
                        value: boolSwitch.checked ? "true" : "false",
                        values: null
                    };
                }

                return null;
            }

            if (valueType === "MultiSelect") {
                const values = Array.from(valueContainer.querySelectorAll("input[name='values']:checked"))
                    .map((x) => String(x.value || "").trim())
                    .filter(Boolean);

                if (values.length === 0) {
                    return null;
                }

                return {
                    criterionDefinitionId: definitionId,
                    value: null,
                    values
                };
            }

            const scalarControl = valueContainer.querySelector("[name='value']");
            if (scalarControl instanceof HTMLInputElement || scalarControl instanceof HTMLSelectElement) {
                const value = String(scalarControl.value || "").trim();
                if (!value) {
                    return null;
                }

                return {
                    criterionDefinitionId: definitionId,
                    value,
                    values: null
                };
            }

            return null;
        }

        function syncCriteriaJson() {
            if (!(criteriaJsonInput instanceof HTMLInputElement)) {
                return;
            }

            criteriaJsonInput.value = JSON.stringify(selectedCriteria);
        }

        function normalizeExistingToSubmitList(existing) {
            return existing
                .map((item) => {
                    const criterionDefinitionId = String(item?.criterionDefinitionId || "").trim();
                    if (!criterionDefinitionId) {
                        return null;
                    }

                    const valueType = String(item?.valueType || definitionsById.get(criterionDefinitionId)?.valueType || "Undefined");
                    const rawValue = String(item?.rawValue || "");

                    if (valueType === "MultiSelect") {
                        const values = parseMulti(rawValue);
                        return values.length > 0
                            ? {
                                criterionDefinitionId,
                                value: null,
                                values
                            }
                            : null;
                    }

                    const value = normalizeScalar(rawValue);
                    if (!value) {
                        return null;
                    }

                    return {
                        criterionDefinitionId,
                        value,
                        values: null
                    };
                })
                .filter((item) => item !== null);
        }

        function syncSelectedName() {
            if (!(selectedNameInput instanceof HTMLInputElement)) {
                return;
            }

            const definition = definitionsById.get(String(definitionSelect.value || ""));
            selectedNameInput.value = definition ? definition.displayName : "Критерий не выбран";
        }

        function parseMulti(rawValue) {
            const normalized = String(rawValue || "").trim();
            if (!normalized) {
                return [];
            }

            try {
                const parsed = JSON.parse(normalized);
                if (Array.isArray(parsed)) {
                    return parsed.map((x) => String(x));
                }
            } catch {
                // ignore
            }

            return normalized.split(",").map((x) => x.trim()).filter(Boolean);
        }

        function normalizeScalar(rawValue) {
            const value = String(rawValue || "").trim();
            if (value.startsWith("\"") && value.endsWith("\"") && value.length > 1) {
                return value.slice(1, -1);
            }
            return value;
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
                    ? item.options.map((option) => ({
                        value: String(option?.value || ""),
                        label: String(option?.label || option?.value || "")
                    }))
                    : []
            }))
            .filter((item) => item.id.length > 0)
            .sort((left, right) => left.displayName.localeCompare(right.displayName));
    }

    function buildOptionsText(definition) {
        const labels = (definition?.options || [])
            .map((option) => String(option.label || option.value || "").trim())
            .filter(Boolean);

        const unique = [...new Set(labels)];
        return unique.length > 0 ? unique.join(", ") : "—";
    }

    function escapeHtml(value) {
        return String(value || "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll("\"", "&quot;")
            .replaceAll("'", "&#39;");
    }

    function mapValueType(valueType) {
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

    global.realEstateCriteriaInit = realEstateCriteriaInit;
})(window);
