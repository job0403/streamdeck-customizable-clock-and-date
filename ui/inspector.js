let websocket = null;
let uuid = null;

const FIELDS = [
    { id: "timeFormat", type: "select", def: "24h" },
    { id: "dateFormat", type: "select", def: "SAT 6 JUN" },
    
    // Time
    { id: "timeFontFamily", type: "select", def: "Inter" },
    { id: "timeFontWeight", type: "select", def: "600" },
    { id: "timeColor", type: "color", def: "#ffffff" },
    { id: "timeSize", type: "slider", def: 57 },
    { id: "timeStretch", type: "slider", def: 0.9 },
    { id: "timeLetterSpacing", type: "slider", def: 0 },
    { id: "timeY", type: "slider", def: 65 },

    // Seconds
    { id: "secFontFamily", type: "select", def: "Inter" },
    { id: "secFontWeight", type: "select", def: "500" },
    { id: "secColor", type: "color", def: "#ffffff" },
    { id: "secSize", type: "slider", def: 25 },
    { id: "secStretch", type: "slider", def: 1.0 },
    { id: "secLetterSpacing", type: "slider", def: 3 },
    { id: "secY", type: "slider", def: 109 },

    // Date
    { id: "dateFontFamily", type: "select", def: "Inter" },
    { id: "dateFontWeight", type: "select", def: "600" },
    { id: "dateColor", type: "color", def: "#ffffff" },
    { id: "dateSize", type: "slider", def: 23 },
    { id: "dateLetterSpacing", type: "slider", def: 0 },
    { id: "dateY", type: "slider", def: 11 }
];

window.connectElgatoStreamDeckSocket = (inPort, inUUID, inRegisterEvent, inInfo, inActionInfo) => {
    uuid = inUUID;
    websocket = new WebSocket(`ws://127.0.0.1:${inPort}`);

    websocket.onopen = () => {
        const registerData = {
            event: inRegisterEvent,
            uuid: inUUID
        };
        websocket.send(JSON.stringify(registerData));
    };

    const actionInfo = JSON.parse(inActionInfo);
    const settings = actionInfo.payload.settings || {};

    // Initialize all fields
    FIELDS.forEach(f => {
        const val = settings[f.id] !== undefined ? settings[f.id] : f.def;
        
        if (f.type === "select") {
            const el = document.getElementById(f.id);
            if (el) {
                el.value = val;
                el.addEventListener("change", saveSettings);
            }
        } 
        else if (f.type === "slider") {
            const el = document.getElementById(f.id);
            const label = document.getElementById(f.id + "-val");
            if (el) {
                el.value = val;
                if (label) label.textContent = val;
                el.addEventListener("input", () => {
                    if (label) label.textContent = el.value;
                    saveSettings();
                });
            }
        }
        else if (f.type === "color") {
            const picker = document.getElementById(f.id + "Picker");
            const input = document.getElementById(f.id);
            if (picker && input) {
                picker.value = val;
                input.value = val;

                picker.addEventListener("input", () => {
                    input.value = picker.value;
                    saveSettings();
                });

                input.addEventListener("input", () => {
                    const hex = input.value.trim();
                    if (/^#[0-9A-F]{6}$/i.test(hex)) {
                        picker.value = hex;
                        saveSettings();
                    }
                });
            }
        }
    });
};

function saveSettings() {
    if (!websocket || websocket.readyState !== WebSocket.OPEN) return;

    const settings = {};
    FIELDS.forEach(f => {
        const el = document.getElementById(f.id);
        if (el) {
            if (f.type === "slider") {
                settings[f.id] = parseFloat(el.value);
            } else {
                settings[f.id] = el.value;
            }
        }
    });

    websocket.send(JSON.stringify({
        event: "setSettings",
        context: uuid,
        payload: settings
    }));
}
