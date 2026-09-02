(function (global) {
    function matchingPageInit(init) {
        if (!init || typeof init !== "object") {
            return;
        }

        setupMinMatchControls();
        setupDesiredTypesSummary();
        setupAreaSelection(init);
        setupCriteriaBuilder(init);
        setupResultsMap(init);
    }

    function setupMinMatchControls() {
        const range = document.getElementById("minMatchRange");
        const input = document.getElementById("minMatchInput");

        if (!range || !input) {
            return;
        }

        range.addEventListener("input", () => {
            input.value = range.value;
        });

        input.addEventListener("input", () => {
            range.value = input.value;
        });
    }

    function setupDesiredTypesSummary() {
        const checkboxes = Array.from(document.querySelectorAll(".js-desired-type-option"));
        const summary = document.getElementById("desiredTypesSummary");
        const toggle = document.getElementById("desiredTypesDropdown");
        const menu = toggle?.nextElementSibling;

        if (checkboxes.length === 0 || !summary || !toggle) {
            return;
        }

        const emptyText = toggle.getAttribute("data-summary-empty") || "Не важно";

        const updateSummary = () => {
            const labels = checkboxes
                .filter((input) => input instanceof HTMLInputElement && input.checked)
                .map((input) => {
                    const label = input.closest("label");
                    const text = label?.querySelector("span")?.textContent?.trim();
                    return text || input.value;
                });

            summary.textContent = labels.length === 0 ? emptyText : labels.join(", ");
        };

        checkboxes.forEach((input) => {
            input.addEventListener("change", updateSummary);
        });

        if (menu instanceof HTMLElement) {
            menu.addEventListener("click", (event) => {
                const target = event.target;
                if (target instanceof HTMLElement) {
                    event.stopPropagation();
                }
            });
        }

        updateSummary();
    }

    function setupAreaSelection(init) {
        const mapContainer = document.getElementById("requirementAreaMap");
        if (!mapContainer || !global.L) {
            return;
        }

        const ignoreArea = document.getElementById("ignoreArea");
        const selectedLat = document.getElementById("selectedLatitude");
        const selectedLng = document.getElementById("selectedLongitude");
        const selectedAreaText = document.getElementById("selectedAreaText");
        const searchModeText = document.getElementById("searchModeText");
        const radiusRangeKm = document.getElementById("radiusRangeKm");
        const radiusInputKm = document.getElementById("radiusInputKm");

        const lat = toFiniteNumber(selectedLat?.value, init.defaultLat ?? 47.0105);
        const lng = toFiniteNumber(selectedLng?.value, init.defaultLng ?? 28.8638);
        const defaultRadiusKm = Math.max(0.1, toFiniteNumber(radiusInputKm?.value, 5));

        const map = L.map(mapContainer);
        map.setView([lat, lng], 12);
        L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 19,
            attribution: "&copy; OpenStreetMap contributors"
        }).addTo(map);

        const marker = L.marker([lat, lng]).addTo(map);
        const circle = L.circle([lat, lng], {
            radius: defaultRadiusKm * 1000,
            color: "#2f74ff",
            fillColor: "#2f74ff",
            fillOpacity: 0.1
        }).addTo(map);

        const setAreaText = (latitude, longitude) => {
            if (selectedAreaText) {
                selectedAreaText.textContent = `Центр: ${latitude.toFixed(4)}, ${longitude.toFixed(4)}`;
            }
        };

        const setModeText = () => {
            const isIgnore = !!ignoreArea?.checked;
            if (searchModeText) {
                searchModeText.textContent = isIgnore
                    ? "Режим: без выбранной области"
                    : "Режим: по выбранной области";
            }

            if (radiusRangeKm) {
                radiusRangeKm.disabled = isIgnore;
            }

            if (radiusInputKm) {
                radiusInputKm.disabled = isIgnore;
            }

            mapContainer.style.opacity = isIgnore ? "0.65" : "1";
        };

        const updateRadius = () => {
            const valueKm = Math.max(0.1, toFiniteNumber(radiusInputKm?.value, 5));
            const meters = valueKm * 1000;
            circle.setRadius(meters);

            if (radiusRangeKm) {
                radiusRangeKm.value = valueKm.toFixed(1);
            }

            if (radiusInputKm) {
                radiusInputKm.value = valueKm.toFixed(1);
            }
        };

        map.on("click", (event) => {
            if (ignoreArea?.checked) {
                return;
            }

            const newLat = event.latlng.lat;
            const newLng = event.latlng.lng;
            marker.setLatLng([newLat, newLng]);
            circle.setLatLng([newLat, newLng]);

            if (selectedLat) {
                selectedLat.value = newLat.toFixed(6);
            }

            if (selectedLng) {
                selectedLng.value = newLng.toFixed(6);
            }

            setAreaText(newLat, newLng);
        });

        radiusRangeKm?.addEventListener("input", () => {
            if (radiusInputKm) {
                radiusInputKm.value = radiusRangeKm.value;
            }
            updateRadius();
        });

        radiusInputKm?.addEventListener("input", () => {
            if (radiusRangeKm) {
                radiusRangeKm.value = radiusInputKm.value;
            }
            updateRadius();
        });

        ignoreArea?.addEventListener("change", setModeText);

        const mapSearchAddress = document.getElementById("mapSearchAddress");
        const mapSearchButton = document.getElementById("mapSearchButton");
        const mapSearchStatus = document.getElementById("mapSearchStatus");

        async function searchByAddress() {
            if (!mapSearchAddress || !mapSearchStatus) return;
            const queryText = mapSearchAddress.value.trim();
            if (!queryText) {
                mapSearchStatus.textContent = "Введите адрес для поиска.";
                mapSearchStatus.className = "form-text mb-2 text-danger";
                return;
            }

            try {
                mapSearchStatus.textContent = "Поиск адреса...";
                mapSearchStatus.className = "form-text mb-2 text-muted";

                const query = new URLSearchParams({
                    format: "jsonv2",
                    limit: "1",
                    q: queryText
                });

                const response = await fetch(`https://nominatim.openstreetmap.org/search?${query.toString()}`);
                if (!response.ok) {
                    throw new Error("search failed");
                }

                const results = await response.json();
                if (!Array.isArray(results) || results.length === 0) {
                    mapSearchStatus.textContent = "Адрес не найден. Уточните запрос.";
                    mapSearchStatus.className = "form-text mb-2 text-danger";
                    return;
                }

                const first = results[0];
                const newLat = Number(first.lat);
                const newLng = Number(first.lon);

                if (ignoreArea && ignoreArea.checked) {
                    ignoreArea.checked = false;
                    setModeText();
                }

                marker.setLatLng([newLat, newLng]);
                circle.setLatLng([newLat, newLng]);
                map.setView([newLat, newLng], 14);

                if (selectedLat) {
                    selectedLat.value = newLat.toFixed(6);
                }

                if (selectedLng) {
                    selectedLng.value = newLng.toFixed(6);
                }

                setAreaText(newLat, newLng);

                mapSearchStatus.textContent = "Карта перемещена к указанному адресу.";
                mapSearchStatus.className = "form-text mb-2 text-success";
            } catch {
                mapSearchStatus.textContent = "Не удалось выполнить поиск адреса.";
                mapSearchStatus.className = "form-text mb-2 text-danger";
            }
        }

        mapSearchButton?.addEventListener("click", searchByAddress);
        mapSearchAddress?.addEventListener("keydown", (e) => {
            if (e.key === "Enter") {
                e.preventDefault();
                searchByAddress();
            }
        });

        setAreaText(lat, lng);
        setModeText();
        setTimeout(() => map.invalidateSize(), 60);
    }

    function setupCriteriaBuilder(init) {
        const pickerPageSize = 20;
        const sourceDefinitions = Array.isArray(init.criteriaDefinitions) ? init.criteriaDefinitions : [];
        const selectedCriteria = Array.isArray(init.selectedCriteria) ? init.selectedCriteria : [];

        const definitions = sourceDefinitions
            .map((item) => ({
                ...item,
                id: String(item?.id ?? ""),
                displayName: String(item?.displayName ?? "").trim(),
                code: String(item?.code ?? "").trim(),
                category: String(item?.category ?? "").trim(),
                description: String(item?.description ?? "").trim(),
                valueType: String(item?.valueType ?? "Undefined"),
                options: Array.isArray(item?.options)
                    ? item.options
                        .map((option) => ({
                            value: String(option?.value ?? ""),
                            label: String(option?.label ?? option?.value ?? "")
                        }))
                        .sort((left, right) => left.label.localeCompare(right.label))
                    : []
            }))
            .filter((item) => item.id.length > 0)
            .sort((left, right) => left.displayName.localeCompare(right.displayName));

        const definitionsById = new Map(definitions.map((item) => [item.id.toLowerCase(), item]));

        const containers = {
            MustHave: document.getElementById("criteriaMustHave"),
            Important: document.getElementById("criteriaImportant"),
            NiceToHave: document.getElementById("criteriaNiceToHave")
        };

        if (!containers.MustHave || !containers.Important || !containers.NiceToHave) {
            return;
        }

        const state = selectedCriteria
            .filter((item) => item && item.criterionDefinitionId)
            .map((item) => ({
                definitionId: String(item.criterionDefinitionId),
                priority: normalizePriority(item.priority),
                value: item.value ?? "",
                values: Array.isArray(item.values) ? item.values : []
            }));

        const pickerModalElement = document.getElementById("criterionPickerModal");
        const pickerList = document.getElementById("criterionPickerList");
        const pickerSearch = document.getElementById("criterionSearchInput");
        const pickerCategory = document.getElementById("criterionCategoryFilter");
        const pickerPagination = document.getElementById("criterionPickerPagination");
        const pickerModal = pickerModalElement && global.bootstrap
            ? new global.bootstrap.Modal(pickerModalElement)
            : null;

        let pendingPriority = "Important";
        let pickerPage = 1;

        function getDefinition(definitionId) {
            return definitionsById.get(String(definitionId).toLowerCase()) ?? null;
        }

        function isSelected(definitionId) {
            return state.some((item) =>
                String(item.definitionId).toLowerCase() === String(definitionId).toLowerCase());
        }

        function buildOptionsText(definition) {
            const labels = (definition?.options ?? [])
                .map((option) => String(option.label || option.value || "").trim())
                .filter(Boolean);

            const unique = [...new Set(labels)];
            return unique.length > 0 ? unique.join(", ") : "-";
        }

        function buildDescriptionText(definition) {
            const value = String(definition?.description ?? "").trim();
            return value.length > 0 ? value : "-";
        }

        function renderDefinitionMeta(definition) {
            const description = buildDescriptionText(definition);
            const optionsText = buildOptionsText(definition);
            return `
                <div class="small text-muted mt-1">${escapeHtml(description)}</div>
                <div class="small mt-1">Варианты: ${escapeHtml(optionsText)}</div>
            `;
        }

        function renderValueEditor(item, definition) {
            const valueType = definition?.valueType ?? "Text";

            if (valueType === "Boolean") {
                const value = String(item.value ?? "").toLowerCase();
                return `<select class="form-select form-select-sm js-criterion-value">
                    <option value="">Не выбрано</option>
                    <option value="true" ${value === "true" ? "selected" : ""}>Да</option>
                    <option value="false" ${value === "false" ? "selected" : ""}>Нет</option>
                </select>`;
            }

            if (valueType === "Number") {
                return `<input type="number" step="0.01" class="form-control form-control-sm js-criterion-value" value="${escapeHtml(item.value ?? "")}" />`;
            }

            if (valueType === "SingleSelect") {
                const options = Array.isArray(definition?.options) ? definition.options : [];
                const optionsHtml = options.map((option) => {
                    const selected = String(item.value ?? "") === String(option.value) ? "selected" : "";
                    return `<option value="${escapeHtml(option.value)}" ${selected}>${escapeHtml(option.label)}</option>`;
                }).join("");

                return `<select class="form-select form-select-sm js-criterion-value">
                    <option value="">Не выбрано</option>${optionsHtml}
                </select>`;
            }

            if (valueType === "MultiSelect") {
                const options = Array.isArray(definition?.options) ? definition.options : [];
                const selectedValues = new Set((item.values ?? []).map((value) => String(value).toLowerCase()));
                const optionsHtml = options.map((option) => {
                    const selected = selectedValues.has(String(option.value).toLowerCase()) ? "selected" : "";
                    return `<option value="${escapeHtml(option.value)}" ${selected}>${escapeHtml(option.label)}</option>`;
                }).join("");

                return `<select class="form-select form-select-sm js-criterion-values" multiple size="4">${optionsHtml}</select>`;
            }

            return `<input type="text" class="form-control form-control-sm js-criterion-value" value="${escapeHtml(item.value ?? "")}" />`;
        }

        function renderState() {
            Object.values(containers).forEach((container) => {
                container.innerHTML = "";
            });

            state.forEach((item, index) => {
                const definition = getDefinition(item.definitionId);
                if (!definition) {
                    return;
                }

                const target = containers[item.priority];
                if (!target) {
                    return;
                }

                const row = document.createElement("div");
                row.className = "criteria-item js-criterion-row";
                row.dataset.index = String(index);
                row.dataset.definitionId = String(item.definitionId);
                row.dataset.priority = item.priority;
                row.dataset.valueType = definition.valueType;

                row.innerHTML = `
                    <div class="d-flex justify-content-between align-items-start gap-2 mb-2">
                        <div>
                            <div class="fw-semibold">${escapeHtml(definition.displayName)}</div>
                            ${renderDefinitionMeta(definition)}
                        </div>
                        <button type="button" class="btn btn-outline-danger btn-sm js-remove-criterion">Удалить</button>
                    </div>
                    <input type="hidden" class="js-criterion-definition-id" />
                    <input type="hidden" class="js-criterion-priority" />
                    <input type="hidden" class="js-criterion-value-type" />
                    ${renderValueEditor(item, definition)}
                `;

                target.appendChild(row);
            });
        }

        function syncStateFromDom() {
            const rows = document.querySelectorAll(".js-criterion-row");
            rows.forEach((rowElement) => {
                if (!(rowElement instanceof HTMLElement)) {
                    return;
                }

                const index = Number(rowElement.dataset.index ?? "-1");
                if (!Number.isFinite(index) || index < 0 || index >= state.length) {
                    return;
                }

                const stateItem = state[index];
                const multiSelect = rowElement.querySelector(".js-criterion-values");
                if (multiSelect instanceof HTMLSelectElement) {
                    stateItem.values = Array.from(multiSelect.selectedOptions)
                        .map((option) => option.value)
                        .filter((value) => value && value.trim().length > 0);
                    stateItem.value = "";
                    return;
                }

                const valueInput = rowElement.querySelector(".js-criterion-value");
                if (valueInput instanceof HTMLInputElement || valueInput instanceof HTMLSelectElement) {
                    stateItem.value = valueInput.value ?? "";
                    stateItem.values = [];
                }
            });
        }

        function renderPickerPagination(totalPages) {
            if (!pickerPagination) {
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
            const query = String(pickerSearch?.value ?? "").trim().toLowerCase();
            const selectedCategory = String(pickerCategory?.value ?? "").trim().toLowerCase();

            return definitions.filter((definition) => {
                if (selectedCategory && definition.category.toLowerCase() !== selectedCategory) {
                    return false;
                }

                if (!query) {
                    return true;
                }

                const optionHaystack = (definition.options ?? [])
                    .map((option) => `${option.label} ${option.value}`)
                    .join(" ")
                    .toLowerCase();

                const haystack = `${definition.displayName} ${definition.description} ${definition.category} ${definition.code} ${optionHaystack}`
                    .toLowerCase();

                return haystack.includes(query);
            });
        }

        function renderPicker() {
            if (!pickerList) {
                return;
            }

            const filtered = getFilteredDefinitions();
            const totalPages = Math.max(1, Math.ceil(filtered.length / pickerPageSize));
            pickerPage = Math.min(Math.max(pickerPage, 1), totalPages);

            const startIndex = (pickerPage - 1) * pickerPageSize;
            const pageItems = filtered.slice(startIndex, startIndex + pickerPageSize);

            pickerList.innerHTML = pageItems.map((definition) => {
                const alreadySelected = isSelected(definition.id);
                return `<button type="button"
                    class="list-group-item list-group-item-action d-flex justify-content-between align-items-start js-pick-criterion"
                    data-definition-id="${definition.id}"
                    ${alreadySelected ? "disabled" : ""}>
                    <span class="me-3 text-start">
                        <span class="fw-semibold d-block">${escapeHtml(definition.displayName)}</span>
                        ${renderDefinitionMeta(definition)}
                    </span>
                    <span class="badge text-bg-light border mt-1">${alreadySelected ? "Добавлен" : "Добавить"}</span>
                </button>`;
            }).join("") || '<div class="small text-muted">Ничего не найдено.</div>';

            renderPickerPagination(totalPages);
        }

        function openPicker(priority) {
            pendingPriority = normalizePriority(priority);
            pickerPage = 1;

            if (pickerSearch) {
                pickerSearch.value = "";
            }

            if (pickerCategory instanceof HTMLSelectElement) {
                pickerCategory.value = "";
            }

            renderPicker();
            pickerModal?.show();
        }

        function initCategoryFilter() {
            if (!(pickerCategory instanceof HTMLSelectElement)) {
                return;
            }

            const categories = [...new Set(
                definitions
                    .map((definition) => definition.category)
                    .filter((category) => category.length > 0)
            )].sort((left, right) => left.localeCompare(right));

            pickerCategory.innerHTML = '<option value="">Не важно</option>';
            categories.forEach((category) => {
                const option = document.createElement("option");
                option.value = category;
                option.textContent = category;
                pickerCategory.appendChild(option);
            });
        }

        document.querySelectorAll(".js-add-criterion").forEach((button) => {
            button.addEventListener("click", () => {
                openPicker(button.getAttribute("data-priority"));
            });
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

            const button = target.closest(".js-picker-page");
            if (!(button instanceof HTMLElement)) {
                return;
            }

            const page = Number(button.dataset.page ?? "1");
            if (!Number.isFinite(page) || page <= 0) {
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

            const definitionId = button.getAttribute("data-definition-id");
            if (!definitionId || isSelected(definitionId)) {
                return;
            }

            syncStateFromDom();
            state.push({
                definitionId,
                priority: pendingPriority,
                value: "",
                values: []
            });

            renderState();
            renderPicker();
            pickerModal?.hide();
        });

        document.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof HTMLElement) || !target.classList.contains("js-remove-criterion")) {
                return;
            }

            const row = target.closest(".js-criterion-row");
            if (!(row instanceof HTMLElement)) {
                return;
            }

            const index = Number(row.dataset.index ?? "-1");
            if (index < 0 || index >= state.length) {
                return;
            }

            syncStateFromDom();
            state.splice(index, 1);
            renderState();
        });

        const requirementForm = document.getElementById("requirementForm");
        requirementForm?.addEventListener("submit", () => {
            syncStateFromDom();
            bindCriteriaInputNames();
        });

        function bindCriteriaInputNames() {
            const rows = document.querySelectorAll(".js-criterion-row");
            rows.forEach((rowElement, index) => {
                if (!(rowElement instanceof HTMLElement)) {
                    return;
                }

                const definitionId = rowElement.dataset.definitionId ?? "";
                const priority = normalizePriority(rowElement.dataset.priority);
                const valueType = rowElement.dataset.valueType ?? "Undefined";

                const definitionInput = rowElement.querySelector(".js-criterion-definition-id");
                const priorityInput = rowElement.querySelector(".js-criterion-priority");
                const valueTypeInput = rowElement.querySelector(".js-criterion-value-type");
                const valueInput = rowElement.querySelector(".js-criterion-value");
                const valuesInput = rowElement.querySelector(".js-criterion-values");

                if (definitionInput instanceof HTMLInputElement) {
                    definitionInput.name = `criteria[${index}].CriterionDefinitionId`;
                    definitionInput.value = definitionId;
                }

                if (priorityInput instanceof HTMLInputElement) {
                    priorityInput.name = `criteria[${index}].Priority`;
                    priorityInput.value = priority;
                }

                if (valueTypeInput instanceof HTMLInputElement) {
                    valueTypeInput.name = `criteria[${index}].ValueType`;
                    valueTypeInput.value = valueType;
                }

                if (valueInput instanceof HTMLInputElement || valueInput instanceof HTMLSelectElement) {
                    valueInput.name = `criteria[${index}].Value`;
                }

                if (valuesInput instanceof HTMLSelectElement) {
                    valuesInput.name = `criteria[${index}].Values`;
                }
            });
        }

        initCategoryFilter();
        renderState();
    }

    function setupResultsMap(init) {
        const mapContainer = document.getElementById("matchingMap");
        if (!mapContainer || !global.L) {
            return;
        }

        const results = Array.isArray(init.matchResults) ? init.matchResults : [];
        if (results.length === 0) {
            return;
        }

        const pricing = init.pricing || {};
        const amountFormatter = new Intl.NumberFormat(pricing.numberCulture || "ru-RU", {
            style: "decimal",
            minimumFractionDigits: 0,
            maximumFractionDigits: 0
        });
        const baseCurrencyCode = pricing.baseCurrencyCode || "USD";
        const showDistance = init.showDistance === true;

        function getDisplayPriceAmount(item) {
            const original = Number(item.originalPriceAmount);
            if (Number.isFinite(original) && original > 0) {
                return original;
            }

            const base = Number(item.price);
            return Number.isFinite(base) ? base : 0;
        }

        function getDisplayCurrencyCode(item) {
            const originalCurrency = typeof item.originalPriceCurrency === "string"
                ? item.originalPriceCurrency.trim()
                : "";

            return originalCurrency || baseCurrencyCode;
        }

        function formatDisplayPrice(item) {
            return `${amountFormatter.format(getDisplayPriceAmount(item))} ${getDisplayCurrencyCode(item)}`;
        }

        function formatBasePriceHint(item) {
            const displayCurrency = getDisplayCurrencyCode(item);
            const basePrice = Number(item.price);

            if (!Number.isFinite(basePrice) || displayCurrency.toUpperCase() === baseCurrencyCode.toUpperCase()) {
                return "";
            }

            return `≈ ${amountFormatter.format(basePrice)} ${baseCurrencyCode}`;
        }

        function formatMatchPercent(value) {
            const score = Number(value);
            return Number.isFinite(score) ? `${Math.round(score * 100)}%` : "-";
        }

        function formatDistanceKm(value) {
            const meters = Number(value);
            return Number.isFinite(meters) ? `${(meters / 1000).toFixed(2)} км` : "-";
        }

        const map = L.map(mapContainer);
        map.setView([47.0105, 28.8638], 12);
        L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 19,
            attribution: "&copy; OpenStreetMap contributors"
        }).addTo(map);

        const markers = new Map();
        const bounds = [];

        results.forEach((item) => {
            if (!Number.isFinite(item.latitude) || !Number.isFinite(item.longitude)) {
                return;
            }

            const marker = L.marker([item.latitude, item.longitude]).addTo(map);
            const basePriceHint = formatBasePriceHint(item);
            marker.bindPopup(`
                <div class="small">
                    <div class="fw-semibold">${escapeHtml(item.title)}</div>
                    <div class="text-muted">${escapeHtml(item.address)}</div>
                    <div>Цена: ${escapeHtml(formatDisplayPrice(item))}</div>
                    ${basePriceHint ? `<div class="text-muted">${escapeHtml(basePriceHint)}</div>` : ""}
                    <div>Площадь: ${Number(item.area).toFixed(1)} м²</div>
                    ${showDistance ? `<div>Расстояние: ${escapeHtml(formatDistanceKm(item.distanceMeters))}</div>` : ""}
                    <div>Совпадение: ${escapeHtml(formatMatchPercent(item.matchScore))}</div>
                    <a href="${escapeHtml(item.detailsUrl)}" class="btn btn-sm btn-primary mt-2 text-white">Открыть объект</a>
                </div>
            `);

            markers.set(String(item.propertyId), marker);
            bounds.push([item.latitude, item.longitude]);
        });

        if (bounds.length === 1) {
            map.setView(bounds[0], 14);
        } else if (bounds.length > 1) {
            map.fitBounds(bounds, { padding: [20, 20], maxZoom: 14 });
        }

        document.querySelectorAll(".js-show-on-map").forEach((button) => {
            button.addEventListener("click", () => {
                const propertyId = button.getAttribute("data-property-id");
                if (!propertyId) {
                    return;
                }

                const marker = markers.get(String(propertyId));
                if (!marker) {
                    return;
                }

                map.setView(marker.getLatLng(), 15);
                marker.openPopup();
                mapContainer.scrollIntoView({ behavior: "smooth", block: "center" });
            });
        });

        setTimeout(() => map.invalidateSize(), 60);
    }

    function toFiniteNumber(value, fallback) {
        const parsed = Number(value);
        return Number.isFinite(parsed) ? parsed : fallback;
    }

    function normalizePriority(priority) {
        if (priority === "MustHave" || priority === "Important" || priority === "NiceToHave") {
            return priority;
        }

        return "Important";
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll("\"", "&quot;")
            .replaceAll("'", "&#39;");
    }

    global.matchingPageInit = matchingPageInit;
})(window);
