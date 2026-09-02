(function (global) {
    function realEstateFormMapInit(config) {
        if (!config || typeof L === "undefined") {
            return;
        }

        const mapElement = document.getElementById(config.mapElementId || "");
        const latInput = document.getElementById(config.latitudeInputId || "");
        const lngInput = document.getElementById(config.longitudeInputId || "");
        const addressInput = document.getElementById(config.addressInputId || "");
        const searchButton = document.getElementById(config.searchButtonId || "");
        const statusElement = document.getElementById(config.statusElementId || "");

        if (!(mapElement instanceof HTMLElement)
            || !(latInput instanceof HTMLInputElement)
            || !(lngInput instanceof HTMLInputElement)
            || !(addressInput instanceof HTMLInputElement)) {
            return;
        }

        const parsedDefaultLat = Number(config.defaultLatitude);
        const parsedDefaultLng = Number(config.defaultLongitude);
        const defaultLat = Number.isFinite(parsedDefaultLat) ? parsedDefaultLat : 47.0105;
        const defaultLng = Number.isFinite(parsedDefaultLng) ? parsedDefaultLng : 28.8638;
        const isReadOnly = !!config.readOnly;

        const map = L.map(mapElement).setView([defaultLat, defaultLng], 13);

        L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 19,
            attribution: "&copy; OpenStreetMap"
        }).addTo(map);

        const marker = L.marker([defaultLat, defaultLng], { draggable: !isReadOnly }).addTo(map);

        function updateCoordinates(lat, lng, updateMap) {
            const normalizedLat = Number(lat);
            const normalizedLng = Number(lng);
            if (Number.isNaN(normalizedLat) || Number.isNaN(normalizedLng)) {
                return;
            }

            latInput.value = normalizedLat.toFixed(6);
            lngInput.value = normalizedLng.toFixed(6);

            marker.setLatLng([normalizedLat, normalizedLng]);
            if (updateMap) {
                map.panTo([normalizedLat, normalizedLng], { animate: true });
            }
        }

        function setStatus(message, isError) {
            if (!(statusElement instanceof HTMLElement)) {
                return;
            }

            statusElement.textContent = message;
            statusElement.classList.toggle("text-danger", !!isError);
        }

        async function reverseGeocode(lat, lng) {
            try {
                const query = new URLSearchParams({
                    format: "jsonv2",
                    lat: String(lat),
                    lon: String(lng)
                });
                const response = await fetch(`https://nominatim.openstreetmap.org/reverse?${query.toString()}`);
                if (!response.ok) {
                    throw new Error("reverse failed");
                }

                const payload = await response.json();
                if (payload && payload.display_name) {
                    addressInput.value = payload.display_name;
                    setStatus("Адрес обновлен по выбранной точке.", false);
                }
            } catch {
                setStatus("Координаты обновлены. Адрес не удалось получить автоматически.", false);
            }
        }

        async function searchByAddress() {
            const queryText = addressInput.value.trim();
            if (!queryText) {
                setStatus("Введите адрес для поиска.", true);
                return;
            }

            try {
                setStatus("Поиск адреса...", false);
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
                    setStatus("Адрес не найден. Уточните запрос.", true);
                    return;
                }

                const first = results[0];
                const lat = Number(first.lat);
                const lng = Number(first.lon);
                updateCoordinates(lat, lng, true);
                if (first.display_name) {
                    addressInput.value = first.display_name;
                }
                setStatus("Точка на карте обновлена по адресу.", false);
            } catch {
                setStatus("Не удалось выполнить поиск адреса.", true);
            }
        }

        if (!isReadOnly) {
            map.on("click", async (event) => {
                updateCoordinates(event.latlng.lat, event.latlng.lng, false);
                await reverseGeocode(event.latlng.lat, event.latlng.lng);
            });

            marker.on("dragend", async () => {
                const position = marker.getLatLng();
                updateCoordinates(position.lat, position.lng, false);
                await reverseGeocode(position.lat, position.lng);
            });

            latInput.addEventListener("change", () => {
                updateCoordinates(latInput.value, lngInput.value, true);
            });
            lngInput.addEventListener("change", () => {
                updateCoordinates(latInput.value, lngInput.value, true);
            });

            searchButton?.addEventListener("click", searchByAddress);
            setStatus("Можно выбрать точку кликом по карте или искать по адресу.", false);
        } else {
            map.dragging.disable();
            map.touchZoom.disable();
            map.doubleClickZoom.disable();
            map.scrollWheelZoom.disable();
            map.boxZoom.disable();
            map.keyboard.disable();
            if (map.tap) {
                map.tap.disable();
            }

            setStatus("Редактирование заявки недоступно.", false);
        }

        setTimeout(() => map.invalidateSize(), 50);
    }

    global.realEstateFormMapInit = realEstateFormMapInit;
})(window);
