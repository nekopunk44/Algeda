(function (global) {
    function homePageInit(init) {
        if (!init || !global.L) {
            return;
        }

        const properties = Array.isArray(init.properties) ? init.properties : [];
        const currencies = Array.isArray(init.currencies) ? init.currencies : [];
        const typeLabels = init.typeLabels || {};
        const pricing = init.pricing || {};
        const detailsTemplate = init.detailsUrlTemplate || "";

        const mapElement = document.getElementById("homePropertiesMap");
        if (!mapElement) {
            return;
        }

        const LIST_PAGE_SIZE = 10;

        const map = L.map(mapElement);
        const defaultCenter = [47.0105, 28.8638];
        map.setView(defaultCenter, 12);

        L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 19,
            attribution: "&copy; OpenStreetMap contributors"
        }).addTo(map);

        const markersLayer = L.layerGroup().addTo(map);
        const markerRenderer = L.canvas({ padding: 0.5 });

        const listContainer = document.getElementById("propertiesList");
        const emptyState = document.getElementById("propertiesEmpty");
        const resultsCount = document.getElementById("resultsCountBadge");
        const resultsRenderHint = document.getElementById("resultsRenderHint");
        const mapLoader = document.getElementById("mapLoader");
        const pagination = document.getElementById("propertiesPagination");
        const paginationNav = document.getElementById("propertiesPaginationNav");

        const filterSearch = document.getElementById("filterSearch");
        const filterType = document.getElementById("filterType");
        const filterMinPrice = document.getElementById("filterMinPrice");
        const filterMaxPrice = document.getElementById("filterMaxPrice");
        const filterPriceCurrency = document.getElementById("filterPriceCurrency");
        const filterMinRooms = document.getElementById("filterMinRooms");
        const filterMaxRooms = document.getElementById("filterMaxRooms");
        const applyFiltersButton = document.getElementById("applyFilters");
        const resetFilters = document.getElementById("resetFilters");

        const amountFormatter = new Intl.NumberFormat(pricing.numberCulture || "ru-RU", {
            style: "decimal",
            minimumFractionDigits: 0,
            maximumFractionDigits: 0
        });
        const baseCurrencyCode = pricing.baseCurrencyCode || "USD";

        const areaFormatter = new Intl.NumberFormat("ru-RU", {
            minimumFractionDigits: 0,
            maximumFractionDigits: 1
        });

        const preparedProperties = properties.map((item) => ({
            ...item,
            _normalizedTitle: normalize(item.title),
            _normalizedAddress: normalize(item.address),
            _previewPhotoPath: getPreviewPhotoPath(item)
        }));

        const propertyById = new Map(preparedProperties.map((item) => [String(item.id), item]));
        const markerByPropertyId = new Map();

        let currentFiltered = preparedProperties;
        let currentPage = 1;

        function toNumber(value) {
            if (value === null || value === undefined || value === "") {
                return null;
            }

            const parsed = Number(value);
            return Number.isFinite(parsed) ? parsed : null;
        }

        function normalize(value) {
            return String(value || "").trim().toLowerCase();
        }

        function detailsUrl(id) {
            return detailsTemplate.replace("__id__", id);
        }

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

        function getFilterRateToBase() {
            const selectedCurrency = typeof filterPriceCurrency?.value === "string"
                ? filterPriceCurrency.value.trim().toUpperCase()
                : baseCurrencyCode;

            const currency = currencies.find((item) =>
                typeof item?.code === "string"
                && item.code.trim().toUpperCase() === selectedCurrency);

            const rate = Number(currency?.rateToBase);
            return Number.isFinite(rate) && rate > 0 ? rate : 1;
        }

        function convertFilterPriceToBase(value, rateToBase) {
            return value === null ? null : value * rateToBase;
        }

        function hasCoordinates(item) {
            return Number.isFinite(item.latitude)
                && Number.isFinite(item.longitude)
                && item.latitude >= -90
                && item.latitude <= 90
                && item.longitude >= -180
                && item.longitude <= 180;
        }

        function getPreviewPhotoPath(item) {
            const mainPhoto = typeof item?.mainPhotoPath === "string" ? item.mainPhotoPath.trim() : "";
            if (mainPhoto.length > 0) {
                return mainPhoto;
            }

            if (!Array.isArray(item?.photoPaths)) {
                return "";
            }

            for (const path of item.photoPaths) {
                if (typeof path === "string" && path.trim().length > 0) {
                    return path.trim();
                }
            }

            return "";
        }

        function buildPopupHtml(item) {
            const lazyPhoto = item._previewPhotoPath
                ? `<img class="home-popup-photo d-none" data-photo-src="${escapeHtml(item._previewPhotoPath)}" alt="Фото объекта" loading="lazy" />`
                : '<div class="home-popup-photo-empty">Фото отсутствует</div>';
            const basePriceHint = formatBasePriceHint(item);

            return `
                <article class="home-map-popup">
                    ${lazyPhoto}
                    <div class="fw-semibold home-popup-title">${escapeHtml(item.title)}</div>
                    <div class="text-muted small mb-1">${escapeHtml(item.address)}</div>
                    <div class="small">Цена: ${escapeHtml(formatDisplayPrice(item))}</div>
                    ${basePriceHint ? `<div class="small text-muted mb-2">${escapeHtml(basePriceHint)}</div>` : `<div class="mb-2"></div>`}
                    <a href="${detailsUrl(item.id)}" class="btn btn-sm btn-primary home-popup-link">Подробнее</a>
                </article>
            `;
        }

        function createMarkersIndex() {
            preparedProperties.forEach((item) => {
                if (!hasCoordinates(item)) {
                    return;
                }

                const marker = L.circleMarker([item.latitude, item.longitude], {
                    radius: 7,
                    weight: 1,
                    color: "#0d6efd",
                    fillColor: "#3d8bfd",
                    fillOpacity: 0.85,
                    renderer: markerRenderer
                });

                marker.bindPopup(buildPopupHtml(item));

                marker.on("popupopen", (event) => {
                    const popupElement = event.popup?.getElement();
                    if (!popupElement) {
                        return;
                    }

                    const lazyImage = popupElement.querySelector(".home-popup-photo[data-photo-src]");
                    if (!(lazyImage instanceof HTMLImageElement)) {
                        return;
                    }

                    if (!lazyImage.getAttribute("src")) {
                        const photoSrc = lazyImage.dataset.photoSrc;
                        if (photoSrc) {
                            lazyImage.setAttribute("src", photoSrc);
                        }
                    }

                    lazyImage.classList.remove("d-none");
                });

                markerByPropertyId.set(String(item.id), marker);
            });
        }

        function renderMarkers(filtered) {
            markersLayer.clearLayers();

            let bounds = null;
            let visibleMarkersCount = 0;

            filtered.forEach((item) => {
                const marker = markerByPropertyId.get(String(item.id));
                if (!marker) {
                    return;
                }

                markersLayer.addLayer(marker);
                const latLng = marker.getLatLng();

                if (!bounds) {
                    bounds = L.latLngBounds(latLng, latLng);
                } else {
                    bounds.extend(latLng);
                }

                visibleMarkersCount++;
            });

            if (visibleMarkersCount === 1 && bounds) {
                map.setView(bounds.getCenter(), 14);
            } else if (visibleMarkersCount > 1 && bounds) {
                map.fitBounds(bounds, { padding: [30, 30], maxZoom: 15 });
            } else {
                map.setView(defaultCenter, 12);
            }
        }

        function getTotalPages() {
            return Math.max(1, Math.ceil(currentFiltered.length / LIST_PAGE_SIZE));
        }

        function renderCardsPage() {
            if (!listContainer) {
                return;
            }

            const totalPages = getTotalPages();
            if (currentPage > totalPages) {
                currentPage = totalPages;
            }

            const start = (currentPage - 1) * LIST_PAGE_SIZE;
            const pageItems = currentFiltered.slice(start, start + LIST_PAGE_SIZE);

            listContainer.innerHTML = pageItems.map((item) => {
                const previewPhoto = item._previewPhotoPath;
                const photoMarkup = previewPhoto
                    ? `<img src="${escapeHtml(previewPhoto)}" alt="Фото объекта" class="property-card-photo" loading="lazy" />`
                    : '<div class="property-card-photo property-card-photo-empty">Фото отсутствует</div>';

                const area = Number.isFinite(item.area) ? areaFormatter.format(item.area) : "0";
                const roomsCount = Number.isFinite(item.roomsCount) ? item.roomsCount : 0;
                const basePriceHint = formatBasePriceHint(item);
                const typeLabel = typeLabels[item.type] || item.type || "Не указано";

                return `
                <div class="col-md-6 col-xl-4">
                    <article class="card h-100 property-card border" data-detail-url="${detailsUrl(item.id)}" tabindex="0">
                        <div class="card-body d-flex flex-column h-100">
                            <div class="property-card-photo-shell mb-3">
                                ${photoMarkup}
                                <span class="badge text-bg-light border property-card-type">${escapeHtml(typeLabel)}</span>
                            </div>
                            <h3 class="h6 mb-1 property-card-title">${escapeHtml(item.title)}</h3>
                            <div class="text-muted small mb-2">${escapeHtml(item.address)}</div>
                            <div class="d-flex flex-wrap gap-2 mb-3 property-card-meta">
                                <span class="badge text-bg-secondary-subtle">${area} м²</span>
                                <span class="badge text-bg-success-subtle">${roomsCount} комн.</span>
                            </div>
                            <div class="d-flex justify-content-between align-items-end gap-2 mt-auto property-card-footer">
                                <div class="d-flex flex-wrap gap-2">
                                    <button type="button" class="btn btn-outline-secondary btn-sm" data-focus-id="${item.id}">На карте</button>
                                    <a class="btn btn-primary btn-sm" href="${detailsUrl(item.id)}">Детали</a>
                                </div>
                                <div class="property-card-price-box text-end">
                                    <div class="property-card-price">${escapeHtml(formatDisplayPrice(item))}</div>
                                    ${basePriceHint ? `<div class="small text-muted">${escapeHtml(basePriceHint)}</div>` : ""}
                                </div>
                            </div>
                        </div>
                    </article>
                </div>
            `;
            }).join("");

            if (resultsRenderHint) {
                if (currentFiltered.length === 0) {
                    resultsRenderHint.textContent = "";
                    return;
                }

                const from = start + 1;
                const to = Math.min(start + LIST_PAGE_SIZE, currentFiltered.length);
                resultsRenderHint.textContent = `Показано ${from}-${to} из ${currentFiltered.length}`;
            }
        }

        function buildPageItems(totalPages, page) {
            if (totalPages <= 7) {
                return Array.from({ length: totalPages }, (_, index) => index + 1);
            }

            if (page <= 3) {
                return [1, 2, 3, 4, "...", totalPages];
            }

            if (page >= totalPages - 2) {
                return [1, "...", totalPages - 3, totalPages - 2, totalPages - 1, totalPages];
            }

            return [1, "...", page - 1, page, page + 1, "...", totalPages];
        }

        function renderPagination() {
            if (!pagination || !paginationNav) {
                return;
            }

            const totalPages = getTotalPages();
            if (totalPages <= 1) {
                pagination.innerHTML = "";
                paginationNav.classList.add("d-none");
                return;
            }

            paginationNav.classList.remove("d-none");

            const pageItems = buildPageItems(totalPages, currentPage);
            const canGoPrev = currentPage > 1;
            const canGoNext = currentPage < totalPages;

            pagination.innerHTML = `
                <li class="page-item ${canGoPrev ? "" : "disabled"}">
                    <button type="button" class="page-link" data-page="${currentPage - 1}" ${canGoPrev ? "" : "disabled"} aria-label="Предыдущая">&laquo;</button>
                </li>
                ${pageItems.map((item) => {
                    if (item === "...") {
                        return '<li class="page-item disabled"><span class="page-link">...</span></li>';
                    }

                    const pageNumber = Number(item);
                    const activeClass = pageNumber === currentPage ? "active" : "";
                    return `
                        <li class="page-item ${activeClass}">
                            <button type="button" class="page-link" data-page="${pageNumber}">${pageNumber}</button>
                        </li>
                    `;
                }).join("")}
                <li class="page-item ${canGoNext ? "" : "disabled"}">
                    <button type="button" class="page-link" data-page="${currentPage + 1}" ${canGoNext ? "" : "disabled"} aria-label="Следующая">&raquo;</button>
                </li>
            `;
        }

        function applyFilters() {
            const search = normalize(filterSearch?.value);
            const type = filterType?.value || "";
            const minPrice = toNumber(filterMinPrice?.value);
            const maxPrice = toNumber(filterMaxPrice?.value);
            const priceRateToBase = getFilterRateToBase();
            const minPriceBase = convertFilterPriceToBase(minPrice, priceRateToBase);
            const maxPriceBase = convertFilterPriceToBase(maxPrice, priceRateToBase);
            const minRooms = toNumber(filterMinRooms?.value);
            const maxRooms = toNumber(filterMaxRooms?.value);

            currentFiltered = preparedProperties.filter((item) => {
                if (type && item.type !== type) {
                    return false;
                }

                if (minPriceBase !== null && item.price < minPriceBase) {
                    return false;
                }

                if (maxPriceBase !== null && item.price > maxPriceBase) {
                    return false;
                }

                if (minRooms !== null && item.roomsCount < minRooms) {
                    return false;
                }

                if (maxRooms !== null && item.roomsCount > maxRooms) {
                    return false;
                }

                if (!search) {
                    return true;
                }

                return item._normalizedTitle.includes(search)
                    || item._normalizedAddress.includes(search);
            });

            currentPage = 1;

            if (resultsCount) {
                resultsCount.textContent = `${currentFiltered.length} из ${preparedProperties.length}`;
            }

            if (emptyState) {
                emptyState.classList.toggle("d-none", currentFiltered.length !== 0);
            }

            renderCardsPage();
            renderPagination();
            renderMarkers(currentFiltered);
        }

        listContainer?.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof HTMLElement)) {
                return;
            }

            const focusButton = target.closest("[data-focus-id]");
            if (focusButton instanceof HTMLElement) {
                const propertyId = String(focusButton.dataset.focusId || "");
                const item = propertyById.get(propertyId);
                const marker = markerByPropertyId.get(propertyId);

                if (item && marker && hasCoordinates(item)) {
                    map.setView([item.latitude, item.longitude], 15);
                    marker.openPopup();
                }

                return;
            }

            if (target.closest("a, button, input, select, label")) {
                return;
            }

            const card = target.closest("[data-detail-url]");
            if (card instanceof HTMLElement && card.dataset.detailUrl) {
                global.location.href = card.dataset.detailUrl;
            }
        });

        listContainer?.addEventListener("keydown", (event) => {
            if (event.key !== "Enter") {
                return;
            }

            const target = event.target;
            if (!(target instanceof HTMLElement)) {
                return;
            }

            const card = target.closest("[data-detail-url]");
            if (card instanceof HTMLElement && card.dataset.detailUrl) {
                global.location.href = card.dataset.detailUrl;
            }
        });

        pagination?.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof HTMLElement)) {
                return;
            }

            const pageButton = target.closest("[data-page]");
            if (!(pageButton instanceof HTMLElement)) {
                return;
            }

            const page = Number(pageButton.dataset.page);
            if (!Number.isFinite(page)) {
                return;
            }

            const nextPage = Math.trunc(page);
            const totalPages = getTotalPages();
            if (nextPage < 1 || nextPage > totalPages || nextPage === currentPage) {
                return;
            }

            currentPage = nextPage;
            renderCardsPage();
            renderPagination();
            listContainer?.scrollIntoView({ behavior: "smooth", block: "start" });
        });

        applyFiltersButton?.addEventListener("click", applyFilters);

        resetFilters?.addEventListener("click", () => {
            if (filterSearch) filterSearch.value = "";
            if (filterType) filterType.value = "";
            if (filterMinPrice) filterMinPrice.value = "";
            if (filterMaxPrice) filterMaxPrice.value = "";
            if (filterPriceCurrency) filterPriceCurrency.value = baseCurrencyCode;
            if (filterMinRooms) filterMinRooms.value = "";
            if (filterMaxRooms) filterMaxRooms.value = "";
            applyFilters();
        });

        filterPriceCurrency?.addEventListener("change", applyFilters);

        createMarkersIndex();
        applyFilters();
        mapLoader?.classList.add("d-none");
        setTimeout(() => map.invalidateSize(), 50);
    }

    function escapeHtml(value) {
        return String(value || "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;");
    }

    global.homePageInit = homePageInit;
})(window);
