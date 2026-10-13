/* =========================================================
   Okçuluk Analiz — tek sayfa arayüz
   Bölümler:
     1. Ortak yardımcılar ve görünüm geçişleri
     2. Video yükleme
     3. Oturum durumu takibi
     4. Geçmiş analizler
     5. Analiz verisi işleme ve oturum özeti
     6. Oynatma kontrolleri ve atış anı yavaşlatma
     7. Canvas üstü kol çizimi ve anlık açılar
     8. Atış senkronizasyonu ve atış panelleri
     9. Sparkline çizimi
   Açılar ve benzerlik değerleri sunucudan gelir; tarayıcıda hesaplanmaz.
   ========================================================= */
document.addEventListener("DOMContentLoaded", () => {
    "use strict";

    const $ = (id) => document.getElementById(id);

    // --- Görünümler ---
    const uploadSection = $("uploadSection");
    const processingSection = $("processingSection");
    const playerSection = $("playerSection");
    const historySection = $("historySection");

    // --- Yükleme ---
    const dropZone = $("dropZone");
    const videoFileInput = $("videoFileInput");
    const selectedFileBox = $("selectedFile");
    const selectedPreview = $("selectedPreview");
    const selectedFileName = $("selectedFileName");
    const selectedFileSize = $("selectedFileSize");
    const btnRemoveFile = $("btnRemoveFile");
    const btnStartAnalysis = $("btnStartAnalysis");

    // --- İşleniyor / hata ---
    const processingBody = $("processingBody");
    const processingBadge = $("processingBadge");
    const processingFileName = $("processingFileName");
    const errorBody = $("errorBody");
    const errorMessage = $("errorMessage");

    // --- Oynatıcı ---
    const video = $("analysisVideo");
    const canvas = $("overlayCanvas");
    const ctx = canvas.getContext("2d");
    const btnPlayPause = $("btnPlayPause");
    const seekBar = $("seekBar");
    const timeCurrent = $("timeCurrent");
    const timeTotal = $("timeTotal");
    const speedButtons = document.querySelectorAll(".btn-speed");
    const btnSlowToggle = $("btnSlowToggle");
    const analysisSpeedSelect = $("analysisSpeedSelect");
    const slowBadge = $("slowBadge");
    const noDataBow = $("noDataBow");
    const noDataDraw = $("noDataDraw");

    const historyList = $("historyList");
    const toastEl = $("toast");

    // Kol renkleri tüm arayüzde aynı (CSS değişkenleriyle eşleşir)
    const ARM = {
        bow: { name: "Yay Kolu", color: "#2F4A38", text: "#FDFBF5", list: () => $("bowArmHistoryList"), counter: () => $("bowPanelCounter"), key: "bowArmSnapshots" },
        draw: { name: "Çekme Kolu", color: "#B8892F", text: "#24312A", list: () => $("drawArmHistoryList"), counter: () => $("drawPanelCounter"), key: "drawArmSnapshots" }
    };

    const GAP_TEXT = "Bazı karelerde kol veya el görünmedi. Bu atışın karşılaştırması eksik olabilir.";

    // --- Durum ---
    let selectedFile = null;
    let previewUrl = null;
    let pollInterval = null;
    let drawLoopId = null;
    let currentAnalysisData = null;
    let sortedFrames = null;
    let currentAnalysisHistory = null;
    let lastRenderedBowSnapshot = -1;
    let lastRenderedDrawSnapshot = -1;
    let activeSparklines = [];
    let selectedShotNumber = null;
    let isSeeking = false;
    let overlaySize = { w: 0, h: 0, dpr: 1 };

    // Atış anında (canlı satır) uygulanan yavaş oynatma; kullanıcı açıp kapatabilir ve hızını seçebilir
    let LIVE_SLOW_RATE = 0.1;
    let slowEnabled = true;
    let liveSlowPrevRate = null; // null: yavaşlatma aktif değil; değer: geri dönülecek hız

    // =========================================================
    // 1. Ortak yardımcılar ve görünüm geçişleri
    // =========================================================
    function showView(name) {
        uploadSection.hidden = name !== "upload";
        processingSection.hidden = name !== "processing";
        playerSection.hidden = name !== "player";
    }

    let toastTimer = null;
    function toast(message) {
        toastEl.textContent = message;
        toastEl.hidden = false;
        clearTimeout(toastTimer);
        toastTimer = setTimeout(() => { toastEl.hidden = true; }, 3500);
    }

    function el(tag, className, text) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined && text !== null) node.textContent = text;
        return node;
    }

    function svgIcon(paths, className = "icon") {
        const ns = "http://www.w3.org/2000/svg";
        const svg = document.createElementNS(ns, "svg");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("class", className);
        svg.setAttribute("aria-hidden", "true");
        paths.forEach(d => {
            const p = document.createElementNS(ns, "path");
            p.setAttribute("d", d);
            svg.appendChild(p);
        });
        return svg;
    }
    const ICON_WARN = ["M12 4 2.8 19.5h18.4z", "M12 10v4.5", "M12 17.2v.01"];
    const ICON_FILM = ["M4 5h16v14H4z", "M8 5v14M16 5v14M4 9h4M4 15h4M16 9h4M16 15h4"];
    const ICON_ARROW = ["M5 12h14", "m13 6 6 6-6 6"];

    function formatClock(sec) {
        if (!isFinite(sec) || sec < 0) sec = 0;
        const m = Math.floor(sec / 60);
        const s = Math.floor(sec % 60);
        return `${m}:${String(s).padStart(2, "0")}`;
    }

    function formatSize(bytes) {
        if (!bytes && bytes !== 0) return "";
        const mb = bytes / (1024 * 1024);
        return mb >= 1 ? `${mb.toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
    }

    function formatDate(iso) {
        const d = new Date(iso);
        if (isNaN(d)) return "—";
        return d.toLocaleDateString("tr-TR") + " " + d.toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" });
    }

    function isStatus(statusValue, target) {
        const map = { Created: 0, VideoUploaded: 1, Analyzing: 2, Completed: 3, Failed: 4 };
        const numTarget = typeof target === "string" ? map[target] : target;
        const numValue = typeof statusValue === "string" ? map[statusValue] : statusValue;
        return numValue === numTarget;
    }

    // Arayüzdeki dört durum: Bekliyor, İşleniyor, Tamamlandı, Hata
    function statusInfo(status) {
        if (isStatus(status, "Completed")) return { text: "Tamamlandı", cls: "badge-completed" };
        if (isStatus(status, "Failed")) return { text: "Hata", cls: "badge-failed" };
        if (isStatus(status, "Analyzing")) return { text: "İşleniyor", cls: "badge-processing" };
        return { text: "Bekliyor", cls: "badge-waiting" };
    }

    // =========================================================
    // 2. Video yükleme
    // =========================================================
    dropZone.addEventListener("click", () => videoFileInput.click());
    dropZone.addEventListener("keydown", (e) => {
        if (e.key === "Enter" || e.key === " ") { e.preventDefault(); videoFileInput.click(); }
    });
    dropZone.addEventListener("dragover", (e) => { e.preventDefault(); dropZone.classList.add("dragover"); });
    dropZone.addEventListener("dragleave", (e) => { e.preventDefault(); dropZone.classList.remove("dragover"); });
    dropZone.addEventListener("drop", (e) => {
        e.preventDefault();
        dropZone.classList.remove("dragover");
        if (e.dataTransfer.files && e.dataTransfer.files.length > 0) handleFileSelect(e.dataTransfer.files[0]);
    });
    videoFileInput.addEventListener("change", (e) => {
        if (e.target.files && e.target.files.length > 0) handleFileSelect(e.target.files[0]);
    });

    function handleFileSelect(file) {
        if (!file.type.startsWith("video/")) {
            toast("Lütfen bir video dosyası seç.");
            return;
        }
        selectedFile = file;
        selectedFileName.textContent = file.name;
        selectedFileSize.textContent = formatSize(file.size);
        if (previewUrl) URL.revokeObjectURL(previewUrl);
        previewUrl = URL.createObjectURL(file);
        selectedPreview.src = previewUrl;
        selectedFileBox.hidden = false;
        btnStartAnalysis.disabled = false;
    }

    btnRemoveFile.addEventListener("click", () => resetUploadUI());

    function resetUploadUI() {
        selectedFile = null;
        videoFileInput.value = "";
        selectedFileBox.hidden = true;
        selectedPreview.removeAttribute("src");
        selectedPreview.load();
        if (previewUrl) { URL.revokeObjectURL(previewUrl); previewUrl = null; }
        btnStartAnalysis.disabled = true;
    }

    btnStartAnalysis.addEventListener("click", () => startAnalysis());

    async function startAnalysis() {
        if (!selectedFile) return;

        showProcessing(selectedFile.name, "Bekliyor");
        try {
            const createRes = await fetch("/api/sessions", { method: "POST" });
            if (!createRes.ok) throw new Error("Oturum oluşturulamadı (" + createRes.status + ").");
            const session = await createRes.json();

            const formData = new FormData();
            formData.append("video", selectedFile);
            const uploadRes = await fetch(`/api/sessions/${session.id}/video`, { method: "POST", body: formData });
            if (!uploadRes.ok) throw new Error("Video yüklenemedi (" + uploadRes.status + ").");

            pollStatus(session.id);
            fetchHistory();
        } catch (err) {
            console.error(err);
            showError(err.message || "Video yüklenirken bir sorun oluştu.");
        }
    }

    // =========================================================
    // 3. Oturum durumu takibi
    // =========================================================
    function showProcessing(fileName, statusText) {
        processingBody.hidden = false;
        errorBody.hidden = true;
        processingFileName.textContent = fileName || "";
        setProcessingBadge(statusText);
        showView("processing");
    }

    function setProcessingBadge(statusText) {
        processingBadge.textContent = statusText;
        processingBadge.className = "badge " + (statusText === "İşleniyor" ? "badge-processing" : "badge-waiting");
    }

    function showError(message) {
        stopPolling();
        processingBody.hidden = true;
        errorBody.hidden = false;
        errorMessage.textContent = message;
        $("btnRetry").hidden = !selectedFile;
        showView("processing");
    }

    $("btnRetry").addEventListener("click", () => startAnalysis());
    $("btnErrorBack").addEventListener("click", () => goToUpload());

    function stopPolling() {
        if (pollInterval) { clearInterval(pollInterval); pollInterval = null; }
    }

    function pollStatus(sessionId) {
        stopPolling();
        pollInterval = setInterval(async () => {
            try {
                const res = await fetch(`/api/sessions/${sessionId}`);
                if (!res.ok) return;
                const data = await res.json();

                if (isStatus(data.status, "Completed")) {
                    stopPolling();
                    loadPlayer(sessionId, data);
                    fetchHistory(); // geçmiş listesindeki durumu güncelle
                } else if (isStatus(data.status, "Failed")) {
                    showError("Analiz sırasında bir hata oluştu. Videoyu tekrar deneyebilir veya başka bir video seçebilirsin.");
                    fetchHistory();
                } else {
                    setProcessingBadge(statusInfo(data.status).text);
                }
            } catch (err) {
                console.error("Durum sorgulanamadı", err);
            }
        }, 3000);
    }

    // =========================================================
    // 4. Geçmiş analizler
    // =========================================================
    fetchHistory();

    async function fetchHistory() {
        try {
            const res = await fetch("/api/sessions");
            if (!res.ok) throw new Error("HTTP " + res.status);
            const sessions = await res.json();

            historyList.innerHTML = "";
            if (sessions.length === 0) {
                historyList.appendChild(el("div", "muted small", "Henüz analiz yok."));
                return;
            }
            sessions.forEach(s => historyList.appendChild(createHistoryCard(s)));
        } catch (err) {
            console.error("Geçmiş alınamadı:", err);
            historyList.innerHTML = "";
            historyList.appendChild(el("div", "muted small", "Geçmiş analizler yüklenemedi."));
        }
    }

    function createHistoryCard(s) {
        const completed = isStatus(s.status, "Completed");
        const info = statusInfo(s.status);
        const card = el("button", "history-card");
        card.type = "button";
        const name = s.videoFileName || "Video";
        card.setAttribute("aria-label", `${name}, ${info.text}${completed ? ", analizi aç" : ""}`);

        const top = el("div", "history-card-top");
        top.appendChild(svgIcon(ICON_FILM));
        const title = el("span", "history-card-title", name);
        title.title = name;
        top.appendChild(title);
        card.appendChild(top);

        const meta = el("div", "history-card-meta");
        meta.appendChild(el("span", "tabular", formatDate(s.createdAt)));
        meta.appendChild(el("span", "badge " + info.cls, info.text));
        card.appendChild(meta);

        if (completed) {
            const action = el("span", "history-card-action", "Analizi aç");
            action.appendChild(svgIcon(ICON_ARROW));
            card.appendChild(action);
            card.addEventListener("click", () => {
                stopPolling();
                loadPlayer(s.id, s);
                window.scrollTo({ top: 0, behavior: "smooth" });
            });
        } else {
            card.addEventListener("click", () => {
                toast(isStatus(s.status, "Failed") ? "Bu analiz hata ile sonuçlandı." : "Bu analiz henüz tamamlanmadı.");
            });
        }
        return card;
    }

    $("navHistory").addEventListener("click", (e) => {
        e.preventDefault();
        historySection.scrollIntoView({ behavior: "smooth", block: "start" });
    });
    $("navNewAnalysis").addEventListener("click", () => goToUpload());
    $("btnClosePlayer").addEventListener("click", () => goToUpload());

    // =========================================================
    // 5. Analiz verisi işleme ve oturum özeti
    // =========================================================
    function goToUpload() {
        stopPolling();
        closePlayer();
        resetUploadUI();
        showView("upload");
        fetchHistory();
        window.scrollTo({ top: 0, behavior: "smooth" });
    }

    function closePlayer() {
        stopDrawLoop();
        video.pause();
        video.removeAttribute("src");
        video.load();
        currentAnalysisData = null;
        sortedFrames = null;
        currentAnalysisHistory = null;
        lastRenderedBowSnapshot = -1;
        lastRenderedDrawSnapshot = -1;
        activeSparklines = [];
        selectedShotNumber = null;
        liveSlowPrevRate = null;
        slowBadge.hidden = true;
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        noDataBow.hidden = true;
        noDataDraw.hidden = true;
    }

    async function loadPlayer(sessionId, sessionMeta) {
        closePlayer();
        showView("player");

        try {
            const res = await fetch(`/api/sessions/${sessionId}/analysis`);
            if (!res.ok) throw new Error("HTTP " + res.status);
            currentAnalysisData = await res.json();
        } catch (err) {
            console.error(err);
            showView("upload");
            toast("Analiz verisi alınamadı.");
            return;
        }

        if (!sessionMeta) {
            try {
                const r = await fetch(`/api/sessions/${sessionId}`);
                if (r.ok) sessionMeta = await r.json();
            } catch { /* özet için isteğe bağlı */ }
        }

        sortedFrames = (currentAnalysisData.frames && currentAnalysisData.frames.length > 0)
            ? [...currentAnalysisData.frames].sort((a, b) => a.timestampSec - b.timestampSec)
            : [];

        renderSummary(sessionMeta, currentAnalysisData);
        renderShotHistory(currentAnalysisData.history || { bowArmSnapshots: [], drawArmSnapshots: [] });

        video.src = `/api/sessions/${sessionId}/video`;
        video.addEventListener("loadedmetadata", () => {
            resizeOverlay();
            seekBar.max = String(video.duration || 0);
            timeTotal.textContent = formatClock(video.duration);
            const active = document.querySelector(".btn-speed.active");
            if (active && active.dataset.speed) video.playbackRate = parseFloat(active.dataset.speed);
            video.play().catch(e => console.warn("Otomatik oynatma engellendi:", e));
        }, { once: true });
        video.onerror = () => {
            if (!video.getAttribute("src")) return;
            toast("Video oynatılamadı (hata kodu: " + (video.error ? video.error.code : "?") + ").");
        };
        video.load();

        stopDrawLoop();
        drawLoop();
    }

    // Bir panelin tüm atışları: son anlık durumun satırları
    function finalRows(armKey) {
        const snaps = currentAnalysisHistory && currentAnalysisHistory[ARM[armKey].key];
        return snaps && snaps.length ? snaps[snaps.length - 1].rows : [];
    }
    function finalOrt(armKey) {
        const snaps = currentAnalysisHistory && currentAnalysisHistory[ARM[armKey].key];
        return snaps && snaps.length ? snaps[snaps.length - 1].ort : null;
    }

    function renderSummary(meta, data) {
        const name = (meta && meta.videoFileName) || "Analiz";
        $("resultTitle").textContent = name;
        $("sumDate").textContent = meta && meta.createdAt ? formatDate(meta.createdAt) : "—";
        $("fpsDisplay").textContent = data.fps ? Math.round(data.fps) : "—";

        currentAnalysisHistory = data.history || null;
        const numbers = new Set([...finalRows("bow"), ...finalRows("draw")].map(r => r.shotNumber));
        $("sumShots").textContent = numbers.size;

        const simText = (armKey) => {
            const rows = finalRows(armKey);
            const ort = finalOrt(armKey);
            if (rows.length < 2 || !ort || ort.similarityPercent == null) return "—";
            return `%${ort.similarityPercent.toFixed(0)}`;
        };
        $("sumBowSim").textContent = simText("bow");
        $("sumDrawSim").textContent = simText("draw");

        const ort = finalOrt("draw") || finalOrt("bow");
        $("sumDuration").textContent = ort && ort.durationSec != null ? `${ort.durationSec.toFixed(2)} sn` : "—";
    }

    // =========================================================
    // 6. Oynatma kontrolleri ve atış anı yavaşlatma
    // =========================================================
    btnPlayPause.addEventListener("click", () => {
        if (video.paused) video.play(); else video.pause();
    });
    video.addEventListener("play", updatePlayButton);
    video.addEventListener("pause", updatePlayButton);
    function updatePlayButton() {
        const playing = !video.paused;
        btnPlayPause.classList.toggle("playing", playing);
        btnPlayPause.setAttribute("aria-label", playing ? "Duraklat" : "Oynat");
    }

    seekBar.addEventListener("input", () => {
        isSeeking = true;
        video.currentTime = parseFloat(seekBar.value);
        updateTimeUI();
    });
    seekBar.addEventListener("change", () => { isSeeking = false; });

    function updateTimeUI() {
        const dur = video.duration || 0;
        if (!isSeeking) seekBar.value = String(video.currentTime);
        const pct = dur > 0 ? (video.currentTime / dur) * 100 : 0;
        seekBar.style.setProperty("--pct", pct + "%");
        timeCurrent.textContent = formatClock(video.currentTime);
    }

    speedButtons.forEach(btn => {
        btn.addEventListener("click", () => {
            speedButtons.forEach(b => { b.classList.remove("active"); b.removeAttribute("aria-pressed"); });
            btn.classList.add("active");
            btn.setAttribute("aria-pressed", "true");
            const speed = parseFloat(btn.dataset.speed);
            // Atış anında yavaşlatma sürüyorsa seçilen hız atış bitince uygulanır
            if (liveSlowPrevRate !== null) { liveSlowPrevRate = speed; return; }
            video.playbackRate = speed;
        });
    });

    analysisSpeedSelect.addEventListener("change", () => {
        LIVE_SLOW_RATE = parseFloat(analysisSpeedSelect.value);
        if (liveSlowPrevRate !== null) video.playbackRate = LIVE_SLOW_RATE;
    });

    btnSlowToggle.addEventListener("click", () => {
        slowEnabled = !slowEnabled;
        btnSlowToggle.classList.toggle("on", slowEnabled);
        btnSlowToggle.setAttribute("aria-checked", String(slowEnabled));
        analysisSpeedSelect.disabled = !slowEnabled;
        updateLiveSlowdown();
    });

    // Atış analiz tablosuna işlenirken (canlı satır) video yavaşlar, atış bitince seçili hıza geri döner.
    function updateLiveSlowdown() {
        const isLive = String(lastRenderedBowSnapshot).startsWith("live-") ||
                       String(lastRenderedDrawSnapshot).startsWith("live-");
        try {
            if (isLive && slowEnabled && liveSlowPrevRate === null) {
                liveSlowPrevRate = video.playbackRate;
                video.playbackRate = LIVE_SLOW_RATE;
            } else if ((!isLive || !slowEnabled) && liveSlowPrevRate !== null) {
                video.playbackRate = liveSlowPrevRate;
                liveSlowPrevRate = null;
            }
        } catch (e) {
            console.warn("Video hızı değiştirilemedi:", e);
            liveSlowPrevRate = null;
        }
        slowBadge.hidden = liveSlowPrevRate === null;
    }

    // =========================================================
    // 7. Canvas üstü kol çizimi ve anlık açılar
    // =========================================================
    window.addEventListener("resize", () => resizeOverlay());

    // Canvas, video öğesinin kutusuyla aynı boyutta; çizim devicePixelRatio ile keskin tutulur
    function resizeOverlay() {
        const w = video.clientWidth;
        const h = video.clientHeight;
        const dpr = window.devicePixelRatio || 1;
        if (w <= 0 || h <= 0) return;
        if (w === overlaySize.w && h === overlaySize.h && dpr === overlaySize.dpr) return;
        overlaySize = { w, h, dpr };
        canvas.width = Math.round(w * dpr);
        canvas.height = Math.round(h * dpr);
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    }

    function stopDrawLoop() {
        if (drawLoopId) { cancelAnimationFrame(drawLoopId); drawLoopId = null; }
    }

    function drawLoop() {
        resizeOverlay();
        ctx.clearRect(0, 0, overlaySize.w, overlaySize.h);

        const currentTime = video.currentTime;

        if (sortedFrames && sortedFrames.length > 0) {
            const frame = findNearestFrame(sortedFrames, currentTime);
            if (frame) {
                drawSkeleton(frame.keypoints);
                drawAngles(frame.keypoints, frame.angles, currentTime);
            }
            if (currentAnalysisHistory) syncShotHistory(currentTime);
        }

        // Sparkline'lar: canlı satırda eğri video zamanına göre kare kare ilerler
        activeSparklines.forEach(item => {
            drawSparkline(item);
            if (item.live) updateLiveValues(item, currentTime);
        });

        updateTimeUI();
        drawLoopId = requestAnimationFrame(drawLoop);
    }

    function findNearestFrame(frames, time) {
        if (!frames || frames.length === 0) return null;
        if (frames.length === 1) return frames[0];

        let lo = 0;
        let hi = frames.length - 1;
        if (time <= frames[0].timestampSec) return frames[0];
        if (time >= frames[hi].timestampSec) return frames[hi];

        while (lo <= hi) {
            const mid = Math.floor((lo + hi) / 2);
            if (frames[mid].timestampSec === time) return frames[mid];
            if (frames[mid].timestampSec < time) lo = mid + 1;
            else hi = mid - 1;
        }
        if (lo >= frames.length) return frames[hi];
        if (hi < 0) return frames[lo];
        return Math.abs(frames[lo].timestampSec - time) <= Math.abs(frames[hi].timestampSec - time) ? frames[lo] : frames[hi];
    }

    // Video piksel koordinatlarından canvas (CSS piksel) koordinatlarına; video kutuya "contain" ile sığar
    function getVideoTransform() {
        const vw = video.videoWidth;
        const vh = video.videoHeight;
        const cw = overlaySize.w;
        const ch = overlaySize.h;
        if (!vw || !vh || !cw || !ch) return { scale: 1, offsetX: 0, offsetY: 0 };

        const videoAspect = vw / vh;
        let renderW, renderH, offsetX, offsetY;
        if (videoAspect > cw / ch) {
            renderW = cw; renderH = cw / videoAspect; offsetX = 0; offsetY = (ch - renderH) / 2;
        } else {
            renderH = ch; renderW = ch * videoAspect; offsetX = (cw - renderW) / 2; offsetY = 0;
        }
        return { scale: renderW / vw, offsetX, offsetY };
    }

    function toCanvas(p, t) {
        return { x: p.x * t.scale + t.offsetX, y: p.y * t.scale + t.offsetY };
    }

    function kp(keypoints, name) {
        return keypoints[name] || keypoints[name.charAt(0).toLowerCase() + name.slice(1)];
    }

    function drawSkeleton(keypoints) {
        if (!keypoints) return;
        const t = getVideoTransform();
        const bowShoulder = kp(keypoints, "BowShoulder");
        const bowElbow = kp(keypoints, "BowElbow");
        const bowWrist = kp(keypoints, "BowWrist");
        const drawShoulder = kp(keypoints, "DrawShoulder");
        const drawElbow = kp(keypoints, "DrawElbow");
        const drawWrist = kp(keypoints, "DrawWrist");

        ctx.lineCap = "round";
        ctx.lineJoin = "round";

        // Omuz hattı: nötr, yarı saydam
        if (bowShoulder && drawShoulder) segment(bowShoulder, drawShoulder, t, "rgba(253, 251, 245, 0.55)", 1.5);

        if (bowShoulder && bowElbow) segment(bowShoulder, bowElbow, t, ARM.bow.color);
        if (bowElbow && bowWrist) segment(bowElbow, bowWrist, t, ARM.bow.color);
        if (drawShoulder && drawElbow) segment(drawShoulder, drawElbow, t, ARM.draw.color);
        if (drawElbow && drawWrist) segment(drawElbow, drawWrist, t, ARM.draw.color);

        [bowShoulder, bowElbow, bowWrist].forEach(p => { if (p) point(p, t, ARM.bow.color); });
        [drawShoulder, drawElbow, drawWrist].forEach(p => { if (p) point(p, t, ARM.draw.color); });
    }

    // İnce renkli çizgi + açık hale (videonun koyu ve açık yerlerinde okunur kalır)
    function segment(p1, p2, t, color, width = 2.5) {
        const a = toCanvas(p1, t);
        const b = toCanvas(p2, t);
        ctx.globalAlpha = 0.9;
        ctx.strokeStyle = "rgba(253, 251, 245, 0.7)";
        ctx.lineWidth = width + 2.5;
        ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke();
        ctx.strokeStyle = color;
        ctx.lineWidth = width;
        ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke();
        ctx.globalAlpha = 1;
    }

    function point(p, t, color) {
        const c = toCanvas(p, t);
        ctx.beginPath();
        ctx.arc(c.x, c.y, 4.5, 0, 2 * Math.PI);
        ctx.fillStyle = color;
        ctx.fill();
        ctx.lineWidth = 1.5;
        ctx.strokeStyle = "#FDFBF5";
        ctx.stroke();
    }

    function drawAngles(keypoints, angles, currentTime) {
        const bowAngle = angles ? angles["BowArmElbow"] : null;
        const drawAngle = angles ? angles["DrawArmElbow"] : null;
        const t = getVideoTransform();

        updateGauge("Bow", bowAngle, currentTime);
        updateGauge("Draw", drawAngle, currentTime);
        noDataBow.hidden = bowAngle != null;
        noDataDraw.hidden = drawAngle != null;

        if (bowAngle != null && keypoints) {
            const e = kp(keypoints, "BowElbow");
            if (e) { const c = toCanvas(e, t); angleCapsule(`${bowAngle.toFixed(0)}°`, c.x, c.y, ARM.bow); }
        }
        if (drawAngle != null && keypoints) {
            const e = kp(keypoints, "DrawElbow");
            if (e) { const c = toCanvas(e, t); angleCapsule(`${drawAngle.toFixed(0)}°`, c.x, c.y, ARM.draw); }
        }
    }

    function angleCapsule(text, cx, cy, arm) {
        ctx.font = "600 12px Inter, system-ui, sans-serif";
        const w = ctx.measureText(text).width + 12;
        const h = 20;
        // Etiket dirseğin sağ üstünde; kenara taşacaksa sola/aşağı alınır
        let x = cx + 10;
        let y = cy - h - 8;
        if (x + w > overlaySize.w - 4) x = cx - w - 10;
        if (y < 4) y = cy + 8;
        x = Math.max(4, x);
        ctx.globalAlpha = 0.92;
        ctx.fillStyle = arm.color;
        roundRect(x, y, w, h, 10);
        ctx.fill();
        ctx.globalAlpha = 1;
        ctx.fillStyle = arm.text;
        ctx.textBaseline = "middle";
        ctx.fillText(text, x + 6, y + h / 2 + 0.5);
        ctx.textBaseline = "alphabetic";
    }

    function roundRect(x, y, w, h, r) {
        ctx.beginPath();
        ctx.moveTo(x + r, y);
        ctx.arcTo(x + w, y, x + w, y + h, r);
        ctx.arcTo(x + w, y + h, x, y + h, r);
        ctx.arcTo(x, y + h, x, y, r);
        ctx.arcTo(x, y, x + w, y, r);
        ctx.closePath();
    }

    const gaugeCache = {};
    function updateGauge(suffix, angle, currentTime) {
        const textEl = $("liveAngle" + suffix);
        const arcEl = $("gauge" + suffix + "Arc");
        const value = angle != null ? angle.toFixed(1) : null;
        if (gaugeCache[suffix] !== value) {
            gaugeCache[suffix] = value;
            if (value === null) {
                textEl.textContent = "Veri yok";
                textEl.classList.add("no-data");
                arcEl.setAttribute("stroke-dasharray", "0 180");
            } else {
                textEl.textContent = `${value}°`;
                textEl.classList.remove("no-data");
                const a = Math.max(0, Math.min(180, angle));
                arcEl.setAttribute("stroke-dasharray", `${a} 180`);
            }
        }
        $("gaugeTime" + suffix).textContent = `${currentTime.toFixed(2)} sn`;
    }

    // =========================================================
    // 8. Atış senkronizasyonu ve atış panelleri
    // =========================================================
    function renderShotHistory(history) {
        currentAnalysisHistory = history;
        lastRenderedBowSnapshot = -1;
        lastRenderedDrawSnapshot = -1;
        activeSparklines = [];
        ["bow", "draw"].forEach(arm => {
            const list = ARM[arm].list();
            list.innerHTML = "";
            list.appendChild(el("div", "shot-empty", "Henüz atış yok."));
            list.classList.toggle("compact", finalRows(arm).length > 8);
        });
        syncShotHistory(video.currentTime);
    }

    function syncShotHistory(currentTime) {
        if (!currentAnalysisHistory) return;

        if (currentAnalysisHistory.bowArmSnapshots) {
            const state = resolvePanelState(currentAnalysisHistory.bowArmSnapshots, currentTime);
            const key = state.key + "|" + selectedShotNumber;
            if (key !== lastRenderedBowSnapshot) {
                lastRenderedBowSnapshot = key;
                activeSparklines = activeSparklines.filter(s => s.arm !== "bow");
                renderSnapshot("bow", state.snapshot, state.live);
            }
        }

        if (currentAnalysisHistory.drawArmSnapshots) {
            const state = resolvePanelState(currentAnalysisHistory.drawArmSnapshots, currentTime);
            const key = state.key + "|" + selectedShotNumber;
            if (key !== lastRenderedDrawSnapshot) {
                lastRenderedDrawSnapshot = key;
                activeSparklines = activeSparklines.filter(s => s.arm !== "draw");
                renderSnapshot("draw", state.snapshot, state.live);
            }
        }

        updateLiveSlowdown();
    }

    // Panelde ne gösterileceğini belirler:
    // - Video bir atışın tam çekiş → bırakma aralığındaysa: önceki atışlar + o atışın CANLI satırı
    // - Değilse: video zamanına kadar tamamlanmış atışlar
    function resolvePanelState(snapshots, time) {
        for (let i = 0; i < snapshots.length; i++) {
            const snap = snapshots[i];
            const row = snap.rows[snap.rows.length - 1];
            if (row.startTimeSec !== null && row.startTimeSec <= time && time < snap.releaseTimestamp) {
                return {
                    key: `live-${snap.shotIndex}`,
                    snapshot: i > 0 ? snapshots[i - 1] : null,
                    live: { record: row, averageRecord: snap.ort }
                };
            }
        }
        const snapshot = findSnapshotForTime(snapshots, time);
        return { key: `done-${snapshot ? snapshot.shotIndex : 0}`, snapshot: snapshot, live: null };
    }

    function findSnapshotForTime(snapshots, time) {
        if (!snapshots || snapshots.length === 0) return null;
        let validSnapshot = null;
        for (let i = 0; i < snapshots.length; i++) {
            if (snapshots[i].releaseTimestamp <= time) validSnapshot = snapshots[i];
            else break;
        }
        return validSnapshot;
    }

    function renderSnapshot(arm, snapshot, live) {
        const list = ARM[arm].list();
        const total = finalRows(arm).length;
        list.innerHTML = "";

        const rows = snapshot && snapshot.rows ? snapshot.rows : [];
        if (rows.length === 0 && !live) {
            list.appendChild(el("div", "shot-empty", total > 0 ? "Henüz atış yok. Video ilerledikçe atışlar burada görünür." : "Bu kol için atış verisi yok."));
            updateCounter(arm, null, total);
            return;
        }

        rows.forEach(record => appendShot(list, arm, record, snapshot.ort, false));

        let liveRow = null;
        if (live) liveRow = appendShot(list, arm, live.record, live.averageRecord, true);

        // Tek atış varken yalnızca referans görünür; ORT satırı en az iki atışta anlamlıdır
        if (rows.length >= 2 && snapshot.ort) {
            list.appendChild(el("hr", "panel-divider"));
            list.appendChild(createShotRow(arm, snapshot.ort, null, { average: true }));
        }

        const current = live ? live.record.shotNumber : rows[rows.length - 1].shotNumber;
        updateCounter(arm, current, total);

        // Canlı satırı liste içinde görünür alana kaydır (sayfanın kendisini kaydırmadan)
        if (liveRow) {
            const top = liveRow.offsetTop - list.offsetTop;
            if (top < list.scrollTop || top + liveRow.offsetHeight > list.scrollTop + list.clientHeight) {
                list.scrollTo({ top: Math.max(0, top - 8), behavior: "smooth" });
            }
        }
    }

    function updateCounter(arm, current, total) {
        const c = ARM[arm].counter();
        if (!total) { c.textContent = ""; return; }
        c.textContent = current ? `Atış #${current} / Toplam ${total}` : `Toplam ${total}`;
    }

    function appendShot(list, arm, record, averageRecord, isLive) {
        const row = createShotRow(arm, record, averageRecord, { live: isLive });
        list.appendChild(row);
        if (selectedShotNumber !== null && record.shotNumber === selectedShotNumber) {
            list.appendChild(createShotDetail(arm, record));
        }
        return row;
    }

    function createShotRow(arm, record, averageRecord, opts) {
        const isAverage = !!opts.average;
        const isLive = !!opts.live;
        const row = el("div", "shot-row");
        row.setAttribute("role", "listitem");
        if (isAverage) row.classList.add("avg");
        if (isLive) row.classList.add("live");
        if (record.hasDataGap) row.classList.add("has-gap");
        if (!isAverage && record.shotNumber === selectedShotNumber) row.classList.add("selected");

        // Etiket
        const label = el("div", "shot-label");
        label.appendChild(el("span", "shot-name", isAverage ? "ORT" : `Atış #${record.shotNumber}`));
        if (isLive) {
            const tag = el("span", "live-tag");
            tag.appendChild(el("span", "live-dot"));
            tag.appendChild(document.createTextNode("Canlı"));
            label.appendChild(tag);
        } else if (isAverage) {
            label.appendChild(el("span", "muted small", "Ortalama"));
        }
        // Eksik veri rozeti adın altında: değer sütunu tüm satırlarda aynı genişlikte kalır
        if (record.hasDataGap && !isAverage) label.appendChild(createGapBadge(record));
        row.appendChild(label);

        // Sparkline
        const wrap = el("div", "spark-wrap");
        const spark = document.createElement("canvas");
        wrap.appendChild(spark);
        row.appendChild(wrap);

        const item = {
            canvas: spark,
            record: record,
            avgSamples: averageRecord ? averageRecord.curveSamples : null,
            color: ARM[arm].color,
            arm: arm,
            live: isLive,
            average: isAverage,
            selected: row.classList.contains("selected")
        };
        activeSparklines.push(item);

        // Değerler
        const values = el("div", "shot-values");
        const span = record.endTimeSec - record.startTimeSec;
        if (isAverage) {
            if (record.durationSec != null) {
                const tEl = el("span", "shot-time tabular", `${record.durationSec.toFixed(2)} sn`);
                tEl.title = "Ortalama atış süresi (tam çekiş → bırakma)";
                values.appendChild(tEl);
            }
        } else if (record.startTimeSec !== null && span > 0) {
            const tEl = el("span", "shot-time tabular", isLive ? `0.00 / ${span.toFixed(2)} sn` : `${span.toFixed(2)} sn`);
            tEl.title = "Atış süresi (tam çekiş → bırakma)";
            values.appendChild(tEl);
            if (isLive) item.timerEl = tEl;
        }

        if (isLive) {
            const angleEl = el("span", "shot-angle tabular");
            angleEl.title = "Bu andaki dirsek açısı";
            values.appendChild(angleEl);
            item.angleEl = angleEl;
        }


        if (record.isReference || (isLive && !record.progressiveSimilarity)) {
            values.appendChild(el("span", "badge badge-ref", "Referans"));
        } else if (isLive) {
            const sim = el("span", "sim tabular");
            sim.title = "Atışın bu ana kadarki kısmının ortalama eğriye benzerliği";
            sim.appendChild(el("span", "sim-label", "Benzerlik"));
            const val = el("span", null, "…");
            sim.appendChild(val);
            values.appendChild(sim);
            item.scoreEl = val;
        } else if (record.similarityPercent != null) {
            const sim = el("span", "sim tabular");
            sim.title = isAverage ? "Atışların ortalama benzerliği (tutarlılık ölçüsü)" : "Ortalama eğriye benzerlik (tutarlılık ölçüsü)";
            sim.appendChild(el("span", "sim-label", isAverage ? "Ort. benzerlik" : "Benzerlik"));
            sim.appendChild(document.createTextNode(`%${record.similarityPercent.toFixed(0)}`));
            values.appendChild(sim);
        }
        row.appendChild(values);

        // Seçim: tıklayınca video o atışa gider, tekrar tıklayınca seçim kalkar
        if (!isAverage) {
            row.tabIndex = 0;
            row.setAttribute("role", "button");
            row.setAttribute("aria-pressed", String(row.classList.contains("selected")));
            row.setAttribute("aria-label", `${ARM[arm].name} atış ${record.shotNumber}${record.hasDataGap ? ", eksik veri" : ""}. Videoda bu atışa git.`);
            row.addEventListener("click", () => toggleShotSelection(record));
            row.addEventListener("keydown", (e) => {
                if (e.key === "Enter" || e.key === " ") { e.preventDefault(); toggleShotSelection(record); }
            });
        }
        return row;
    }

    function createGapBadge(record) {
        const tip = el("span", "tip");
        const badge = el("span", "gap-badge");
        badge.tabIndex = 0;
        badge.setAttribute("aria-label", "Eksik veri: " + GAP_TEXT);
        badge.appendChild(svgIcon(ICON_WARN));
        badge.appendChild(document.createTextNode("Eksik veri"));
        tip.appendChild(badge);
        tip.appendChild(el("span", "tip-bubble", "Bu atışın bazı karelerinde kol veya el noktaları görünmediği için karşılaştırma eksik olabilir."));
        // Dokunmatik ekranda: dokununca açıklama açılır, satır seçimi tetiklenmez
        tip.addEventListener("click", (e) => { e.stopPropagation(); tip.classList.toggle("open"); });
        tip.addEventListener("keydown", (e) => { if (e.key === "Enter" || e.key === " ") e.stopPropagation(); });
        return tip;
    }

    function toggleShotSelection(record) {
        if (selectedShotNumber === record.shotNumber) {
            selectedShotNumber = null;
        } else {
            selectedShotNumber = record.shotNumber;
            if (record.startTimeSec != null) video.currentTime = record.startTimeSec + 0.01;
        }
        // Paneller bir sonraki karede seçime göre yeniden çizilir
        lastRenderedBowSnapshot = -1;
        lastRenderedDrawSnapshot = -1;
    }

    function createShotDetail(arm, record) {
        const box = el("div", "shot-detail");
        const c = document.createElement("canvas");
        c.setAttribute("role", "img");
        c.setAttribute("aria-label", `Atış #${record.shotNumber} eğrisi, diğer atışlarla birlikte`);
        box.appendChild(c);

        const legend = el("div", "shot-detail-legend tabular");
        if (record.startTimeSec != null) legend.appendChild(el("span", null, `Tam çekiş: ${record.startTimeSec.toFixed(2)} sn`));
        if (record.endTimeSec != null) legend.appendChild(el("span", null, `Bırakma: ${record.endTimeSec.toFixed(2)} sn`));
        if (record.maxDataGapSec > 0 && record.hasDataGap) legend.appendChild(el("span", null, `En uzun veri boşluğu: ${record.maxDataGapSec.toFixed(2)} sn`));
        legend.appendChild(el("span", null, "Soluk kesikli çizgiler: diğer atışlar"));
        box.appendChild(legend);

        const actions = el("div", "shot-detail-actions");
        const clear = el("button", "link-btn", "Seçimi kaldır");
        clear.type = "button";
        clear.addEventListener("click", () => toggleShotSelection(record));
        actions.appendChild(clear);
        box.appendChild(actions);

        requestAnimationFrame(() => drawComparison(c, arm, record));
        return box;
    }

    // Canlı satır için 0..1 arası ilerleme (tam çekiş → bırakma)
    function liveProgress(record, currentTime) {
        const span = record.endTimeSec - record.startTimeSec;
        if (!(span > 0)) return 0;
        return Math.max(0, Math.min(1, (currentTime - record.startTimeSec) / span));
    }

    // Eğri üzerinde kesirli indekste lineer ara değer (sunucudaki örnekler arasında gösterim için)
    function sampleAt(samples, fIdx) {
        const i = Math.floor(fIdx);
        if (i >= samples.length - 1) return samples[samples.length - 1];
        const frac = fIdx - i;
        return samples[i] + (samples[i + 1] - samples[i]) * frac;
    }

    function updateLiveValues(item, currentTime) {
        if (item.timerEl) {
            const span = item.record.endTimeSec - item.record.startTimeSec;
            const elapsed = Math.max(0, Math.min(span, currentTime - item.record.startTimeSec));
            item.timerEl.textContent = `${elapsed.toFixed(2)} / ${span.toFixed(2)} sn`;
        }
        const samples = item.record.curveSamples;
        if (!samples || samples.length === 0) return;
        const fIdx = liveProgress(item.record, currentTime) * (samples.length - 1);

        if (item.angleEl) item.angleEl.textContent = `${sampleAt(samples, fIdx).toFixed(0)}°`;
        if (item.scoreEl) {
            const progressive = item.record.progressiveSimilarity;
            const value = progressive[Math.min(progressive.length - 1, Math.round(fIdx))];
            item.scoreEl.textContent = `%${value.toFixed(0)}`;
        }
    }

    // =========================================================
    // 9. Sparkline çizimi
    // =========================================================
    // Canvas arka tamponunu görünen boyuta ve devicePixelRatio'ya eşitler; çizim CSS pikselinde yapılır
    function prepareCanvas(c) {
        const dpr = window.devicePixelRatio || 1;
        const w = c.clientWidth;
        const h = c.clientHeight;
        if (w === 0 || h === 0) return null;
        const bw = Math.round(w * dpr);
        const bh = Math.round(h * dpr);
        if (c.width !== bw || c.height !== bh) { c.width = bw; c.height = bh; }
        const g = c.getContext("2d");
        g.setTransform(dpr, 0, 0, dpr, 0, 0);
        g.clearRect(0, 0, w, h);
        return { g, w, h };
    }

    function curveRange(curves) {
        let min = Infinity, max = -Infinity;
        curves.forEach(s => { if (s) s.forEach(v => { if (v < min) min = v; if (v > max) max = v; }); });
        let range = max - min;
        if (!isFinite(range) || range === 0) { min -= 10; max += 10; }
        else if (range < 1) { min -= 5; max += 5; }
        else { min -= range * 0.1; max += range * 0.1; }
        return { min, range: max - min };
    }

    function tracePath(g, samples, w, h, r, upTo) {
        const n = samples.length;
        const last = upTo === undefined ? n - 1 : Math.floor(upTo);
        for (let i = 0; i <= last; i++) {
            const x = (i / (n - 1)) * w;
            const y = h - ((samples[i] - r.min) / r.range) * h;
            if (i === 0) g.moveTo(x, y); else g.lineTo(x, y);
        }
    }

    function drawSparkline(item) {
        const samples = item.record.curveSamples;
        if (!samples || samples.length < 2) return;
        const p = prepareCanvas(item.canvas);
        if (!p) return;
        const { g, w, h } = p;
        const pad = 3;
        const ih = h - pad * 2;
        const r = curveRange([samples, item.avgSamples]);
        g.save();
        g.translate(0, pad);

        // Ortalama eğri: arka planda kesikli
        if (item.avgSamples && item.avgSamples.length > 1 && !item.average) {
            g.beginPath();
            g.strokeStyle = "rgba(36, 49, 42, 0.32)";
            g.setLineDash([3, 3]);
            g.lineWidth = 1;
            tracePath(g, item.avgSamples, w, ih, r);
            g.stroke();
            g.setLineDash([]);
        }

        g.beginPath();
        g.strokeStyle = item.color;
        g.lineWidth = item.selected ? 2.6 : 1.8;
        g.lineJoin = "round";
        g.lineCap = "round";
        if (item.average) { g.setLineDash([5, 3]); g.globalAlpha = 0.8; }

        const t = video.currentTime;
        if (item.live) {
            // Canlı: eğri yalnızca video zamanına kadar çizilir, ucu iki örnek arasında akıcı ilerler
            const fIdx = liveProgress(item.record, t) * (samples.length - 1);
            tracePath(g, samples, w, ih, r, fIdx);
            const tipX = (fIdx / (samples.length - 1)) * w;
            const tipY = ih - ((sampleAt(samples, fIdx) - r.min) / r.range) * ih;
            g.lineTo(tipX, tipY);
            g.stroke();
            marker(g, tipX, tipY);
            g.restore();
            return;
        }

        tracePath(g, samples, w, ih, r);
        g.stroke();
        g.setLineDash([]);
        g.globalAlpha = 1;

        // Video bu atışın içindeyse veya geçtiyse konum işareti
        const start = item.record.startTimeSec;
        const end = item.record.endTimeSec;
        if (!item.average && start != null && end != null && t >= start) {
            const progress = t <= end ? (t - start) / (end - start) : 1;
            const idx = Math.floor(progress * (samples.length - 1));
            const x = (idx / (samples.length - 1)) * w;
            const y = ih - ((samples[idx] - r.min) / r.range) * ih;
            marker(g, x, y, t > end);
        }
        g.restore();
    }

    function marker(g, x, y, done) {
        g.beginPath();
        g.arc(x, y, done ? 2.5 : 3.5, 0, 2 * Math.PI);
        g.fillStyle = done ? "rgba(36, 49, 42, 0.55)" : "#B4552D";
        g.fill();
    }

    // Seçili atış ayrıntısı: diğer atışlar soluk kesikli, seçili atış belirgin
    function drawComparison(c, arm, record) {
        const p = prepareCanvas(c);
        if (!p) return;
        const { g, w, h } = p;
        const pad = 8;
        const ih = h - pad * 2;
        const others = finalRows(arm).filter(r => r.shotNumber !== record.shotNumber && r.curveSamples && r.curveSamples.length > 1);
        const r = curveRange([record.curveSamples, ...others.map(o => o.curveSamples)]);
        g.save();
        g.translate(0, pad);

        others.forEach(o => {
            g.beginPath();
            g.strokeStyle = "rgba(107, 98, 82, 0.35)";
            g.setLineDash([3, 3]);
            g.lineWidth = 1;
            tracePath(g, o.curveSamples, w, ih, r);
            g.stroke();
        });
        g.setLineDash([]);

        if (record.curveSamples && record.curveSamples.length > 1) {
            g.beginPath();
            g.strokeStyle = ARM[arm].color;
            g.lineWidth = 2.6;
            g.lineJoin = "round";
            tracePath(g, record.curveSamples, w, ih, r);
            g.stroke();
        }
        g.restore();
    }
});
