/**
 * HanaMedia Global Session Manager & Timeout Interceptor
 * Enforces real-time Session Timeout (Inactivity Timeout), 401 Interceptors,
 * Session Expired Modal Popup, Multi-Tab Broadcast Sync, and Advance Warning.
 */

(function () {
    'use strict';

    if (window.HanaMediaSessionManagerInitialized) return;
    window.HanaMediaSessionManagerInitialized = true;

    var sessionState = {
        authenticated: false,
        expiresAt: null,
        remainingSeconds: 0,
        serverTimeOffset: 0,
        timerId: null,
        warningShown: false,
        channel: null
    };

    window.isSessionExpiredModalOpen = false;

    // BroadcastChannel for multi-tab sync
    if (typeof BroadcastChannel !== 'undefined') {
        try {
            sessionState.channel = new BroadcastChannel('hanamedia_session_channel');
            sessionState.channel.onmessage = function (event) {
                if (event && event.data && event.data.type === 'SESSION_EXPIRED') {
                    showSessionExpiredModal();
                } else if (event && event.data && event.data.type === 'SESSION_RENEWED') {
                    if (event.data.expiresAt) {
                        initSessionTimer(new Date(event.data.expiresAt), event.data.remainingSeconds);
                    }
                }
            };
        } catch (e) {
            console.warn('[SessionManager] BroadcastChannel not supported:', e);
        }
    }

    // LocalStorage fallback for multi-tab sync
    window.addEventListener('storage', function (e) {
        if (e.key === 'hanamedia_session_event' && e.newValue) {
            try {
                var data = JSON.parse(e.newValue);
                if (data.type === 'SESSION_EXPIRED') {
                    showSessionExpiredModal();
                } else if (data.type === 'SESSION_RENEWED' && data.expiresAt) {
                    initSessionTimer(new Date(data.expiresAt), data.remainingSeconds);
                }
            } catch (err) { }
        }
    });

    // 1. GLOBAL INTERCEPTORS FOR FETCH & XHR
    var originalFetch = window.fetch;
    if (originalFetch) {
        window.fetch = function () {
            return originalFetch.apply(this, arguments).then(function (response) {
                if (response && (response.status === 401 || (response.status === 403 && response.url.includes('/api/')))) {
                    showSessionExpiredModal();
                }
                return response;
            });
        };
    }

    var originalXhrOpen = XMLHttpRequest.prototype.open;
    var originalXhrSend = XMLHttpRequest.prototype.send;

    XMLHttpRequest.prototype.open = function () {
        this._xhrUrl = arguments[1];
        return originalXhrOpen.apply(this, arguments);
    };

    XMLHttpRequest.prototype.send = function () {
        var xhr = this;
        var onStateChange = function () {
            if (xhr.readyState === 4) {
                if (xhr.status === 401 || (xhr.status === 403 && xhr._xhrUrl && xhr._xhrUrl.includes('/api/'))) {
                    showSessionExpiredModal();
                }
            }
        };
        xhr.addEventListener('readystatechange', onStateChange, false);
        return originalXhrSend.apply(this, arguments);
    };

    // 2. CHECK SESSION STATUS FROM BACKEND
    function fetchSessionStatus() {
        if (window.location.pathname.toLowerCase() === '/login' ||
            window.location.pathname.toLowerCase() === '/accessdenied') {
            return;
        }

        var xhr = new XMLHttpRequest();
        xhr.open('GET', '/api/auth/session', true);
        xhr.setRequestHeader('Accept', 'application/json');
        xhr.onload = function () {
            if (xhr.status === 200) {
                try {
                    var data = JSON.parse(xhr.responseText);
                    if (data.authenticated && data.expiresAt) {
                        sessionState.authenticated = true;
                        var serverTime = new Date(data.serverTime).getTime();
                        sessionState.serverTimeOffset = Date.now() - serverTime;
                        initSessionTimer(new Date(data.expiresAt), data.remainingSeconds);
                    }
                } catch (e) {
                    console.error('[SessionManager] Error parsing session status:', e);
                }
            } else if (xhr.status === 401) {
                showSessionExpiredModal();
            }
        };
        xhr.send();
    }

    // 3. TIMER & COUNTDOWN LOGIC
    function initSessionTimer(expiresAtDate, initialRemainingSecs) {
        if (sessionState.timerId) {
            clearInterval(sessionState.timerId);
        }

        sessionState.expiresAt = expiresAtDate;
        sessionState.remainingSeconds = initialRemainingSecs;
        hideWarningToast();

        sessionState.timerId = setInterval(function () {
            var nowServerTime = Date.now() - sessionState.serverTimeOffset;
            var remainingMs = sessionState.expiresAt.getTime() - nowServerTime;
            var remainingSecs = Math.max(0, Math.floor(remainingMs / 1000));

            sessionState.remainingSeconds = remainingSecs;
            updateCountdownUI(remainingSecs);

            // Advance 1-Minute Warning
            if (remainingSecs <= 60 && remainingSecs > 0 && !window.isSessionExpiredModalOpen) {
                showWarningToast(remainingSecs);
            } else if (remainingSecs > 60) {
                hideWarningToast();
            }

            // Session Expired
            if (remainingSecs <= 0) {
                clearInterval(sessionState.timerId);
                hideWarningToast();
                showSessionExpiredModal();
            }
        }, 1000);
    }

    function formatTime(seconds) {
        var mins = Math.floor(seconds / 60);
        var secs = seconds % 60;
        return (mins < 10 ? '0' + mins : mins) + ':' + (secs < 10 ? '0' + secs : secs);
    }

    function updateCountdownUI(remainingSecs) {
        var elText = document.getElementById('sessionCountdownText');
        if (elText) {
            elText.textContent = formatTime(remainingSecs);
        }
        var elBadge = document.getElementById('sessionCountdownBadge');
        if (elBadge) {
            elBadge.textContent = 'Phiên còn: ' + formatTime(remainingSecs);
        }
    }

    // 4. 1-MINUTE ADVANCE WARNING TOAST
    function showWarningToast(remainingSecs) {
        var toast = document.getElementById('sessionWarningToast');
        if (!toast) {
            toast = document.createElement('div');
            toast.id = 'sessionWarningToast';
            toast.style.cssText = 'position:fixed; top:16px; right:16px; z-index:999990; background:#FFFBEB; border:1px solid #FCD34D; color:#92400E; padding:12px 18px; border-radius:10px; box-shadow:0 10px 25px -5px rgba(0,0,0,0.15); display:flex; align-items:center; gap:12px; font-size:13.5px; font-weight:600; animation:modalFadeIn 0.3s ease;';
            document.body.appendChild(toast);
        }

        toast.innerHTML = '<i class="ti ti-clock-hour-4" style="font-size:20px; color:#D97706;"></i>' +
            '<div>Phiên làm việc của bạn sẽ hết hạn sau <span id="toastTime" style="font-family:monospace; font-weight:800; color:#B45309;">' + formatTime(remainingSecs) + '</span></div>' +
            '<button type="button" id="btnRenewSession" style="background:#D97706; color:#FFF; border:none; padding:6px 14px; border-radius:6px; font-size:12.5px; font-weight:700; cursor:pointer;">Gia hạn ngay</button>';

        var btnRenew = document.getElementById('btnRenewSession');
        if (btnRenew) {
            btnRenew.onclick = renewSession;
        }
    }

    function hideWarningToast() {
        var toast = document.getElementById('sessionWarningToast');
        if (toast && toast.parentNode) {
            toast.parentNode.removeChild(toast);
        }
    }

    function renewSession() {
        var xhr = new XMLHttpRequest();
        xhr.open('POST', '/api/auth/renew', true);
        xhr.setRequestHeader('Accept', 'application/json');
        xhr.onload = function () {
            if (xhr.status === 200) {
                try {
                    var data = JSON.parse(xhr.responseText);
                    if (data.success && data.expiresAt) {
                        initSessionTimer(new Date(data.expiresAt), data.remainingSeconds);
                        broadcastEvent('SESSION_RENEWED', { expiresAt: data.expiresAt, remainingSeconds: data.remainingSeconds });
                    }
                } catch (e) { }
            }
        };
        xhr.send();
    }

    // 5. SESSION EXPIRED MODAL (SINGLE INSTANCE)
    function showSessionExpiredModal() {
        if (window.isSessionExpiredModalOpen) return;
        window.isSessionExpiredModalOpen = true;

        hideWarningToast();
        broadcastEvent('SESSION_EXPIRED');

        var existingModal = document.getElementById('sessionExpiredModal');
        if (existingModal && existingModal.parentNode) {
            existingModal.parentNode.removeChild(existingModal);
        }

        var backdrop = document.createElement('div');
        backdrop.id = 'sessionExpiredModal';
        backdrop.style.cssText = 'position:fixed; top:0; left:0; width:100vw; height:100vh; background:rgba(15,23,42,0.8); backdrop-filter:blur(6px); z-index:999999; display:flex; align-items:center; justify-content:center; animation:modalFadeIn 0.25s ease-out forwards;';

        backdrop.innerHTML =
            '<div style="background:#FFFFFF; border-radius:18px; width:92%; max-width:440px; padding:32px 24px 24px 24px; box-shadow:0 25px 50px -12px rgba(15,23,42,0.5); text-align:center; animation:modalPopIn 0.3s cubic-bezier(0.175,0.885,0.32,1.275) forwards;">' +
            '<div style="width:72px; height:72px; border-radius:50%; background:#FEE2E2; color:#DC2626; display:inline-flex; align-items:center; justify-content:center; font-size:38px; margin:0 auto 16px auto;">' +
            '<i class="ti ti-lock"></i>' +
            '</div>' +
            '<div style="font-size:20px; font-weight:800; color:#0F172A; margin-bottom:8px;">Phiên làm việc đã hết hạn</div>' +
            '<div style="font-size:14px; color:#475569; line-height:1.6; margin-bottom:24px;">' +
            'Vì lý do bảo mật, phiên đăng nhập của bạn đã hết hạn do không có thao tác.<br>Vui lòng đăng nhập lại để tiếp tục sử dụng hệ thống.' +
            '</div>' +
            '<button type="button" id="btnReloginSubmit" style="width:100%; padding:13px; font-size:14.5px; font-weight:700; background:#DC2626; color:#FFFFFF; border:none; border-radius:10px; cursor:pointer; box-shadow:0 4px 12px rgba(220,38,38,0.3); transition:all 0.15s ease;">' +
            '<i class="ti ti-login"></i> Đăng nhập lại' +
            '</button>' +
            '</div>';

        document.body.appendChild(backdrop);

        var btn = document.getElementById('btnReloginSubmit');
        if (btn) {
            btn.onclick = function () {
                window.location.href = '/Login';
            };
        }

        // Prevent ESC key or outside click from closing modal
        window.addEventListener('keydown', function (e) {
            if (window.isSessionExpiredModalOpen && (e.key === 'Escape' || e.keyCode === 27)) {
                e.preventDefault();
                e.stopPropagation();
            }
        }, true);
    }

    function broadcastEvent(type, payload) {
        var data = Object.assign({ type: type, timestamp: Date.now() }, payload || {});
        if (sessionState.channel) {
            try { sessionState.channel.postMessage(data); } catch (e) { }
        }
        try {
            localStorage.setItem('hanamedia_session_event', JSON.stringify(data));
        } catch (e) { }
    }

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', fetchSessionStatus);
    } else {
        fetchSessionStatus();
    }
})();
