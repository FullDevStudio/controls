// Animated backgrounds for Shiny.Blazor.Controls.
//
// Every effect here mirrors the MAUI host's renderer of the same name (src/Shiny.Maui.Controls/
// AnimatedBackgrounds) step for step, so a background looks the same on both. One canvas per
// background, one requestAnimationFrame loop per background, and the loop only runs while the
// background is running, on screen, in a visible tab and motion is allowed.

const instances = new Map();
const TAU = Math.PI * 2;
const between = (min, max) => min + Math.random() * (max - min);
const clamp = (v, min, max) => Math.min(max, Math.max(min, v));
const countFor = (density, w, h, cap = 2000) =>
    density > 0 && w > 0 && h > 0 ? Math.round(clamp(density * w * h / 10000, 0, cap)) : 0;
const pick = (colors, i, fallback) => colors && colors.length ? colors[Math.abs(i) % colors.length] : fallback;
const reducedMotionQuery = typeof matchMedia === 'function' ? matchMedia('(prefers-reduced-motion: reduce)') : null;

function reconcile(items, target, create) {
    if (items.length > target) items.length = target;
    while (items.length < target) items.push(create());
}

// --- Colour -------------------------------------------------------------------------------------
// Canvas alpha is applied through rgba(), so any CSS colour the caller gives is parsed once into
// its channels by letting a scratch context normalise it.

const colorCache = new Map();
let scratch = null;

function rgba(color, alpha = 1) {
    let c = colorCache.get(color);
    if (!c) {
        scratch = scratch || document.createElement('canvas').getContext('2d');
        scratch.fillStyle = '#000';
        scratch.fillStyle = color;
        const v = scratch.fillStyle;
        if (v[0] === '#') {
            c = [parseInt(v.slice(1, 3), 16), parseInt(v.slice(3, 5), 16), parseInt(v.slice(5, 7), 16), 1];
        } else {
            const m = v.match(/[\d.]+/g) || [0, 0, 0, 1];
            c = [+m[0], +m[1], +m[2], m.length > 3 ? +m[3] : 1];
        }
        colorCache.set(color, c);
    }
    return `rgba(${c[0]},${c[1]},${c[2]},${clamp(c[3] * alpha, 0, 1)})`;
}

// --- Effects -------------------------------------------------------------------------------------
// Each factory returns { sync(first), step(dt), draw(ctx), settle() } reading its options live from
// `s.o`, the box from `s.w`/`s.h`, the clock from `s.time` and the pointer from `s.pointer`.

const effects = {
    snow(s) {
        const flakes = [];
        const spawn = y => {
            const o = s.o, min = Math.max(0.1, Math.min(o.minSize, o.maxSize)), max = Math.max(min, o.maxSize);
            return { x: between(0, s.w), y, r: between(min, max), phase: between(0, TAU), a: between(0.45, 1) };
        };
        return {
            sync(first) {
                if (first) flakes.length = 0;
                reconcile(flakes, countFor(s.o.density, s.w, s.h), () => spawn(between(0, s.h)));
            },
            step(dt) {
                const o = s.o, max = Math.max(o.maxSize, 0.1);
                for (const f of flakes) {
                    const depth = f.r / max;
                    f.y += (18 + depth * 42) * dt;
                    f.x += (o.wind * 50 * depth + Math.cos(s.time * 1.2 + f.phase) * 14 * o.sway) * dt;
                    if (f.y - f.r > s.h) { const n = spawn(0); n.y = -n.r; n.phase = f.phase; Object.assign(f, n); }
                    if (f.x < -f.r) f.x += s.w + f.r * 2;
                    else if (f.x > s.w + f.r) f.x -= s.w + f.r * 2;
                }
            },
            draw(ctx) {
                for (const f of flakes) {
                    ctx.fillStyle = rgba(s.o.color, f.a);
                    ctx.beginPath(); ctx.arc(f.x, f.y, f.r, 0, TAU); ctx.fill();
                }
            }
        };
    },

    rain(s) {
        const drops = [], ripples = [], RIPPLE_LIFE = 0.35;
        const spawn = y => {
            const slant = Math.tan(s.o.angle * Math.PI / 180) * s.h;
            return { x: between(Math.min(0, -slant), s.w + Math.max(0, -slant)), y, depth: between(0.35, 1) };
        };
        return {
            sync(first) {
                if (first) drops.length = 0;
                reconcile(drops, countFor(s.o.density, s.w, s.h), () => spawn(between(-s.h * 0.1, s.h)));
                if (!s.o.splash) ripples.length = 0;
            },
            step(dt) {
                const o = s.o, rad = o.angle * Math.PI / 180, dx = Math.sin(rad), dy = Math.cos(rad);
                for (const d of drops) {
                    const speed = (450 + d.depth * 550) * dt;
                    d.x += dx * speed; d.y += dy * speed;
                    if (d.y > s.h) {
                        if (o.splash && d.depth > 0.6 && d.x >= 0 && d.x <= s.w) ripples.push({ x: d.x, age: 0, size: 3 + d.depth * 5 });
                        Object.assign(d, spawn(-o.length * d.depth));
                    }
                }
                for (let i = ripples.length - 1; i >= 0; i--) {
                    ripples[i].age += dt;
                    if (ripples[i].age >= RIPPLE_LIFE) ripples.splice(i, 1);
                }
            },
            draw(ctx) {
                const o = s.o, rad = o.angle * Math.PI / 180, dx = Math.sin(rad), dy = Math.cos(rad);
                ctx.lineCap = 'round';
                for (const d of drops) {
                    const len = o.length * d.depth;
                    ctx.strokeStyle = rgba(o.color, d.depth * 0.8);
                    ctx.lineWidth = o.thickness * (0.5 + d.depth * 0.5);
                    ctx.beginPath(); ctx.moveTo(d.x, d.y); ctx.lineTo(d.x - dx * len, d.y - dy * len); ctx.stroke();
                }
                ctx.lineWidth = 1;
                for (const r of ripples) {
                    const t = r.age / RIPPLE_LIFE, w = r.size * 2 * (0.4 + t);
                    ctx.strokeStyle = rgba(o.color, 0.6 * (1 - t));
                    ctx.beginPath(); ctx.ellipse(r.x, s.h - 1, w / 2, w * 0.15, 0, 0, TAU); ctx.stroke();
                }
            }
        };
    },

    meteors(s) {
        const meteors = [];
        const dir = () => { const r = s.o.angle * Math.PI / 180; return [Math.cos(r), Math.sin(r)]; };
        const launch = m => {
            const [dx, dy] = dir(), diag = Math.hypot(s.w, s.h), back = diag * 0.6;
            m.x = between(0, s.w) - dx * back;
            m.y = between(0, s.h) - dy * back;
            m.travelled = 0;
            m.distance = back * 2 + s.o.length;
            m.speed = between(260, 520);
            m.wait = between(0, Math.max(s.o.maxDelay, 0));
            return m;
        };
        return {
            sync(first) {
                if (first) meteors.length = 0;
                const count = s.o.density > 0 ? Math.max(1, countFor(s.o.density, s.w, s.h, 200)) : 0;
                reconcile(meteors, count, () => launch({}));
            },
            step(dt) {
                const [dx, dy] = dir();
                for (const m of meteors) {
                    if (m.wait > 0) { m.wait -= dt; continue; }
                    const step = m.speed * dt;
                    m.x += dx * step; m.y += dy * step; m.travelled += step;
                    if (m.travelled >= m.distance) launch(m);
                }
            },
            draw(ctx) {
                const o = s.o, [dx, dy] = dir(), th = Math.max(o.thickness, 0.25), len = Math.max(o.length, 1);
                for (const m of meteors) {
                    if (m.wait > 0) continue;
                    const alpha = clamp(Math.min(m.travelled, m.distance - m.travelled) / 80, 0, 1);
                    if (alpha <= 0) continue;
                    const tx = m.x - dx * len, ty = m.y - dy * len, nx = -dy * th / 2, ny = dx * th / 2;
                    const g = ctx.createLinearGradient(m.x, m.y, tx, ty);
                    g.addColorStop(0, rgba(o.color, alpha));
                    g.addColorStop(1, rgba(o.color, 0));
                    ctx.fillStyle = g;
                    ctx.beginPath(); ctx.moveTo(m.x + nx, m.y + ny); ctx.lineTo(m.x - nx, m.y - ny); ctx.lineTo(tx, ty); ctx.closePath(); ctx.fill();
                    ctx.fillStyle = rgba(o.color, alpha);
                    ctx.beginPath(); ctx.arc(m.x, m.y, th, 0, TAU); ctx.fill();
                }
            }
        };
    },

    bubbles(s) {
        const bubbles = [];
        const spawn = y => {
            const o = s.o, min = Math.max(0.5, Math.min(o.minSize, o.maxSize)), max = Math.max(min, o.maxSize);
            return { x: between(0, s.w), y, r: between(min, max), phase: between(0, TAU), rise: between(18, 46), color: Math.floor(Math.random() * 64) };
        };
        return {
            sync(first) {
                if (first) bubbles.length = 0;
                reconcile(bubbles, countFor(s.o.density, s.w, s.h, 600), () => spawn(between(0, s.h)));
            },
            step(dt) {
                for (const b of bubbles) {
                    b.y -= b.rise * dt;
                    b.x += Math.sin(s.time * 1.5 + b.phase) * 12 * dt;
                    if (b.y + b.r < 0) { const n = spawn(0); Object.assign(b, n, { y: s.h + n.r, phase: b.phase }); }
                }
            },
            draw(ctx) {
                ctx.lineWidth = 1;
                for (const b of bubbles) {
                    const c = pick(s.o.colors, b.color, '#ffffff');
                    ctx.beginPath(); ctx.arc(b.x, b.y, b.r, 0, TAU);
                    ctx.fillStyle = rgba(c, 0.12); ctx.fill();
                    ctx.strokeStyle = rgba(c, 0.6); ctx.stroke();
                    ctx.fillStyle = 'rgba(255,255,255,0.45)';
                    ctx.beginPath(); ctx.arc(b.x - b.r * 0.35, b.y - b.r * 0.35, Math.max(b.r * 0.18, 0.75), 0, TAU); ctx.fill();
                }
            }
        };
    },

    fireflies(s) {
        const flies = [];
        return {
            sync(first) {
                if (first) flies.length = 0;
                reconcile(flies, countFor(s.o.density, s.w, s.h, 400), () => ({
                    x: between(0, s.w), y: between(0, s.h), heading: between(0, TAU), turn: between(-1, 1),
                    phase: between(0, TAU), pulse: between(0.8, 2), color: Math.floor(Math.random() * 64)
                }));
            },
            step(dt) {
                for (const f of flies) {
                    f.turn = clamp(f.turn + between(-2, 2) * dt, -1.2, 1.2);
                    f.heading += f.turn * dt;
                    f.x += Math.cos(f.heading) * 16 * dt;
                    f.y += Math.sin(f.heading) * 16 * dt;
                    if (f.x < -10) f.x = s.w + 10; else if (f.x > s.w + 10) f.x = -10;
                    if (f.y < -10) f.y = s.h + 10; else if (f.y > s.h + 10) f.y = -10;
                }
            },
            draw(ctx) {
                const size = Math.max(s.o.size, 0.25), glow = Math.max(s.o.glow, 0) * size;
                for (const f of flies) {
                    const c = pick(s.o.colors, f.color, '#ffff00');
                    const b = 0.25 + 0.75 * (0.5 + 0.5 * Math.sin(s.time * f.pulse + f.phase));
                    if (glow > size) {
                        const g = ctx.createRadialGradient(f.x, f.y, 0, f.x, f.y, glow);
                        g.addColorStop(0, rgba(c, 0.55 * b));
                        g.addColorStop(1, rgba(c, 0));
                        ctx.fillStyle = g;
                        ctx.beginPath(); ctx.arc(f.x, f.y, glow, 0, TAU); ctx.fill();
                    }
                    ctx.fillStyle = rgba(c, b);
                    ctx.beginPath(); ctx.arc(f.x, f.y, size, 0, TAU); ctx.fill();
                }
            }
        };
    },

    particles(s) {
        const ps = [];
        return {
            sync(first) {
                if (first) ps.length = 0;
                const o = s.o, min = Math.max(0.25, Math.min(o.minSize, o.maxSize)), max = Math.max(min, o.maxSize);
                reconcile(ps, countFor(o.density, s.w, s.h, 400), () => {
                    const a = between(0, TAU), v = between(6, 22);
                    return { x: between(0, s.w), y: between(0, s.h), vx: Math.cos(a) * v, vy: Math.sin(a) * v, r: between(min, max), color: Math.floor(Math.random() * 64) };
                });
            },
            step(dt) {
                const o = s.o, at = s.pointer, reach = Math.max(o.pointerRadius, 1);
                for (const p of ps) {
                    let x = p.x + p.vx * dt, y = p.y + p.vy * dt;
                    if (at && o.pointerMode !== 'none') {
                        const dx = x - at.x, dy = y - at.y, d = Math.hypot(dx, dy);
                        if (d < reach && d > 0.01) {
                            const force = (1 - d / reach) * 160 * dt * (o.pointerMode === 'repel' ? 1 : -1);
                            x += dx / d * force; y += dy / d * force;
                        }
                    }
                    if (x < 0) { x = 0; p.vx = Math.abs(p.vx); } else if (x > s.w) { x = s.w; p.vx = -Math.abs(p.vx); }
                    if (y < 0) { y = 0; p.vy = Math.abs(p.vy); } else if (y > s.h) { y = s.h; p.vy = -Math.abs(p.vy); }
                    p.x = x; p.y = y;
                }
            },
            draw(ctx) {
                const o = s.o, link = o.linkDistance;
                if (link > 0 && o.linkOpacity > 0) {
                    const line = o.linkColor || pick(o.colors, 0, '#ffffff'), l2 = link * link;
                    ctx.lineWidth = 1;
                    for (let i = 0; i < ps.length; i++) {
                        const a = ps[i];
                        for (let j = i + 1; j < ps.length; j++) {
                            const b = ps[j], dx = a.x - b.x, dy = a.y - b.y, d2 = dx * dx + dy * dy;
                            if (d2 >= l2) continue;
                            ctx.strokeStyle = rgba(line, (1 - Math.sqrt(d2) / link) * o.linkOpacity);
                            ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke();
                        }
                    }
                }
                for (const p of ps) {
                    ctx.fillStyle = pick(o.colors, p.color, '#ffffff');
                    ctx.beginPath(); ctx.arc(p.x, p.y, p.r, 0, TAU); ctx.fill();
                }
            }
        };
    },

    aurora(s) {
        const offset = Math.random() * 200;
        const glowAt = (index, time) => {
            const i = index + 1, fx = 0.05 + 0.023 * i, fy = 0.04 + 0.017 * i;
            return [0.5 + 0.42 * Math.sin(time * fx + i * 1.7), 0.38 + 0.3 * Math.sin(time * fy + i * 2.3), 0.85 + 0.15 * Math.sin(time * 0.11 + i)];
        };
        return {
            draw(ctx) {
                const colors = s.o.colors || [];
                const intensity = clamp(s.o.intensity, 0, 1), base = Math.max(s.o.size, 0.05) * Math.max(s.w, s.h);
                colors.forEach((c, i) => {
                    const [fx, fy, scale] = glowAt(i, s.time + offset), r = base * scale, cx = fx * s.w, cy = fy * s.h;
                    const g = ctx.createRadialGradient(cx, cy, 0, cx, cy, r);
                    g.addColorStop(0, rgba(c, intensity));
                    g.addColorStop(0.45, rgba(c, intensity * 0.45));
                    g.addColorStop(1, rgba(c, 0));
                    ctx.fillStyle = g;
                    ctx.beginPath(); ctx.arc(cx, cy, r, 0, TAU); ctx.fill();
                });
            }
        };
    },

    waves(s) {
        const surface = (layer, x, time, amp, wl) => {
            const k = TAU / Math.max(wl, 1), dir = layer % 2 === 0 ? 1 : -1, w = 0.9 + layer * 0.25;
            return amp * Math.sin(x * k + time * w * dir + layer * 1.3) + amp * 0.35 * Math.sin(x * k * 1.9 - time * w * 1.4 * dir + layer);
        };
        return {
            draw(ctx) {
                const o = s.o, colors = o.colors || [], opacity = clamp(o.opacity, 0, 1), amp = Math.max(o.amplitude, 0);
                const top = s.h * clamp(o.level, 0, 1), gap = Math.max(amp * 0.9, 6), STEP = 8;
                colors.forEach((c, layer) => {
                    const rest = top + layer * gap;
                    ctx.beginPath(); ctx.moveTo(0, s.h);
                    for (let x = 0; x <= s.w + STEP; x += STEP) {
                        const px = Math.min(x, s.w);
                        ctx.lineTo(px, rest + surface(layer, px, s.time, amp, o.wavelength));
                    }
                    ctx.lineTo(s.w, s.h); ctx.closePath();
                    ctx.fillStyle = rgba(c, opacity); ctx.fill();
                });
            }
        };
    },

    dotPattern(s) {
        const wanderOffset = Math.random() * 100;
        let spot = { x: 0, y: 0 }, level = 0, placed = false;
        const target = () => {
            const wander = {
                x: s.w * (0.5 + 0.38 * Math.sin((s.time + wanderOffset) * 0.31)),
                y: s.h * (0.5 + 0.36 * Math.sin((s.time + wanderOffset) * 0.23 + 1.1))
            };
            switch (s.o.spotlight) {
                case 'pointer': return s.pointer ? [s.pointer, true] : [spot, false];
                case 'wander': return [wander, true];
                case 'auto': return [s.pointer || wander, true];
                default: return [spot, false];
            }
        };
        const settle = () => { const [t, lit] = target(); spot = { x: t.x, y: t.y }; level = lit ? 1 : 0; placed = true; };
        return {
            settle,
            step(dt) {
                if (!placed) settle();
                const [t, lit] = target(), k = 1 - Math.exp(-9 * dt);
                spot = { x: spot.x + (t.x - spot.x) * k, y: spot.y + (t.y - spot.y) * k };
                level += ((lit ? 1 : 0) - level) * k;
            },
            draw(ctx) {
                if (!placed) settle();
                const o = s.o, spacing = Math.max(o.spacing, 2), size = Math.max(o.dotSize, 0.2), light = o.spotlightColor || o.color;
                const baseAlpha = clamp(o.dotOpacity, 0, 1), on = o.spotlight !== 'none' && level > 0.01, radius = Math.max(o.spotlightRadius, 1);

                if (on && o.glow > 0) {
                    const r = radius * 1.4, g = ctx.createRadialGradient(spot.x, spot.y, 0, spot.x, spot.y, r);
                    g.addColorStop(0, rgba(light, clamp(o.glow, 0, 1) * level));
                    g.addColorStop(1, rgba(light, 0));
                    ctx.fillStyle = g;
                    ctx.beginPath(); ctx.arc(spot.x, spot.y, r, 0, TAU); ctx.fill();
                }

                const cols = Math.floor(s.w / spacing) + 1, rows = Math.floor(s.h / spacing) + 1;
                const ox = (s.w - (cols - 1) * spacing) / 2, oy = (s.h - (rows - 1) * spacing) / 2;

                if (o.pulse) {
                    for (let r = 0; r < rows; r++) for (let c = 0; c < cols; c++) {
                        const x = ox + c * spacing, y = oy + r * spacing, wave = 0.5 + 0.5 * Math.sin((x + y) * 0.02 - s.time * 2.2);
                        ctx.fillStyle = rgba(o.color, baseAlpha * (0.35 + 0.65 * wave));
                        ctx.beginPath(); ctx.arc(x, y, size, 0, TAU); ctx.fill();
                    }
                } else {
                    ctx.fillStyle = rgba(o.color, baseAlpha);
                    ctx.beginPath();
                    for (let r = 0; r < rows; r++) for (let c = 0; c < cols; c++) {
                        const x = ox + c * spacing, y = oy + r * spacing;
                        ctx.moveTo(x + size, y); ctx.arc(x, y, size, 0, TAU);
                    }
                    ctx.fill();
                }

                if (!on) return;
                const c0 = Math.max(0, Math.floor((spot.x - radius - ox) / spacing)), c1 = Math.min(cols - 1, Math.ceil((spot.x + radius - ox) / spacing));
                const r0 = Math.max(0, Math.floor((spot.y - radius - oy) / spacing)), r1 = Math.min(rows - 1, Math.ceil((spot.y + radius - oy) / spacing));
                for (let r = r0; r <= r1; r++) for (let c = c0; c <= c1; c++) {
                    const x = ox + c * spacing, y = oy + r * spacing, d = Math.hypot(x - spot.x, y - spot.y);
                    if (d >= radius) continue;
                    let t = (1 - d / radius) * level; t *= t;
                    ctx.fillStyle = rgba(light, Math.min(1, baseAlpha + t * (1 - baseAlpha)));
                    ctx.beginPath(); ctx.arc(x, y, size * (1 + t * 0.9), 0, TAU); ctx.fill();
                }
            }
        };
    },

    noise(s) {
        // The grain is a few pre-rendered tiles cycled at the frame rate - the same trick as the MAUI
        // host, cheaper here still because a tile is one putImageData and then a pattern fill.
        const FRAMES = 4, TILE = 192;
        let tiles = [], key = '', clock = 0, frame = 0, ox = 0, oy = 0;
        const build = () => {
            const o = s.o, grain = Math.max(o.grainSize, 0.5), coverage = clamp(o.coverage, 0, 1);
            const tw = Math.ceil(Math.min(s.w, TILE)), th = Math.ceil(Math.min(s.h, TILE));
            const k = `${tw}|${th}|${grain}|${coverage}|${o.color}`;
            if (k === key && tiles.length) return;
            key = k;
            tiles = [];
            if (tw <= 0 || th <= 0) return;
            const c = colorChannels(o.color);
            for (let f = 0; f < FRAMES; f++) {
                const tile = document.createElement('canvas');
                tile.width = tw; tile.height = th;
                const tctx = tile.getContext('2d');
                const img = tctx.createImageData(tw, th);
                const cols = Math.ceil(tw / grain), rows = Math.ceil(th / grain);
                for (let r = 0; r < rows; r++) for (let col = 0; col < cols; col++) {
                    const roll = Math.random();
                    if (roll >= coverage) continue;
                    const a = Math.round(255 * c[3] * (roll < coverage * 0.4 ? 1 : 0.45));
                    const x0 = Math.floor(col * grain), y0 = Math.floor(r * grain), x1 = Math.min(tw, Math.floor((col + 1) * grain)), y1 = Math.min(th, Math.floor((r + 1) * grain));
                    for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) {
                        const i = (y * tw + x) * 4;
                        img.data[i] = c[0]; img.data[i + 1] = c[1]; img.data[i + 2] = c[2]; img.data[i + 3] = a;
                    }
                }
                tctx.putImageData(img, 0, 0);
                tiles.push(tile);
            }
        };
        return {
            sync() { build(); },
            step(dt) {
                const rate = Math.max(s.o.frameRate, 0);
                if (rate <= 0) return;
                clock += dt;
                if (clock < 1 / rate) return;
                clock = 0;
                frame = (frame + 1) % FRAMES;
                if (tiles[0]) { ox = between(0, tiles[0].width); oy = between(0, tiles[0].height); }
            },
            draw(ctx) {
                build();
                const tile = tiles[frame];
                if (!tile) return;
                ctx.globalAlpha = clamp(s.o.intensity, 0, 1);
                ctx.translate(-ox, -oy);
                ctx.fillStyle = ctx.createPattern(tile, 'repeat');
                ctx.fillRect(0, 0, s.w + ox, s.h + oy);
            }
        };
    },

    starfield(s) {
        const stars = [];
        let warp = false;
        const place = (st, z) => {
            if (warp) { st.x = between(-1, 1); st.y = between(-1, 1); st.z = z; }
            else { st.x = between(0, s.w); st.y = between(0, s.h); st.z = between(0.2, 1); }
        };
        const project = (x, y, z) => { const half = Math.max(s.w, s.h) / 2; return [s.w / 2 + x / z * half, s.h / 2 + y / z * half]; };
        return {
            sync(first) {
                if (first) stars.length = 0;
                if (warp !== !!s.o.warp) { warp = !!s.o.warp; stars.length = 0; }
                reconcile(stars, countFor(s.o.density, s.w, s.h, 1500), () => {
                    const st = { phase: between(0, TAU), rate: between(0.6, 2.4) };
                    place(st, between(0.05, 1));
                    return st;
                });
            },
            step(dt) {
                for (const st of stars) {
                    if (warp) {
                        st.z -= 0.35 * dt;
                        const [px, py] = project(st.x, st.y, Math.max(st.z, 0.001));
                        if (st.z <= 0.02 || px < -20 || px > s.w + 20 || py < -20 || py > s.h + 20) place(st, 1);
                    } else {
                        st.x -= 4 * st.z * dt;
                        if (st.x < -2) { st.x = s.w + 2; st.y = between(0, s.h); }
                    }
                }
            },
            draw(ctx) {
                const o = s.o, max = Math.max(o.maxSize, 0.2);
                ctx.lineCap = 'round';
                for (const st of stars) {
                    const tw = o.twinkle ? 0.55 + 0.45 * Math.sin(s.time * st.rate + st.phase) : 1;
                    if (warp) {
                        const z = Math.max(st.z, 0.02), near = clamp(1 - z, 0, 1);
                        const [x, y] = project(st.x, st.y, z), [tx, ty] = project(st.x, st.y, Math.min(z + 0.06, 1.2));
                        ctx.strokeStyle = rgba(o.color, Math.min(1, near * 1.6) * tw);
                        ctx.lineWidth = Math.max(0.5, max * near * 1.4);
                        ctx.beginPath(); ctx.moveTo(tx, ty); ctx.lineTo(x, y); ctx.stroke();
                    } else {
                        ctx.fillStyle = rgba(o.color, st.z * tw);
                        ctx.beginPath(); ctx.arc(st.x, st.y, Math.max(0.35, max * st.z), 0, TAU); ctx.fill();
                    }
                }
            }
        };
    },

    matrix(s) {
        const DEFAULT = 'アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワン0123456789';
        const columns = [];
        let rows = 0;
        const cell = () => Math.max(s.o.fontSize, 4);
        const glyph = () => { const set = s.o.characters || DEFAULT; return set[Math.floor(Math.random() * set.length)]; };
        return {
            sync(first) {
                if (first) columns.length = 0;
                rows = Math.ceil(s.h / cell()) + 1;
                reconcile(columns, Math.ceil(s.w / cell()), () => ({ head: between(-rows, rows), rate: between(6, 16), wait: 0, glyphs: [] }));
                for (const c of columns) while (c.glyphs.length < rows) c.glyphs.push(glyph());
                for (const c of columns) c.glyphs.length = rows;
            },
            step(dt) {
                const trail = Math.max(s.o.trailLength, 1);
                for (const c of columns) {
                    if (c.wait > 0) { c.wait -= dt; continue; }
                    c.head += c.rate * dt;
                    if (c.head - trail > rows) { c.head = 0; c.rate = between(6, 16); c.wait = between(0, 2.5); }
                    if (c.glyphs.length && Math.random() < dt * 6) c.glyphs[Math.floor(Math.random() * c.glyphs.length)] = glyph();
                }
            },
            draw(ctx) {
                const o = s.o, size = cell(), trail = Math.max(o.trailLength, 1);
                ctx.font = `${size * 0.9}px ui-monospace, monospace`;
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                columns.forEach((c, x) => {
                    if (c.wait > 0 || !c.glyphs.length) return;
                    const head = Math.floor(c.head);
                    for (let i = 0; i < trail; i++) {
                        const row = head - i;
                        if (row < 0 || row >= c.glyphs.length) continue;
                        ctx.fillStyle = i === 0 ? o.headColor : rgba(o.color, (1 - i / trail) * 0.9);
                        ctx.fillText(c.glyphs[row], x * size + size / 2, row * size + size / 2);
                    }
                });
            }
        };
    }
};

function colorChannels(color) {
    const m = rgba(color, 1).match(/[\d.]+/g);
    return [+m[0], +m[1], +m[2], +m[3]];
}

// --- Host ----------------------------------------------------------------------------------------

function createRenderer(s) {
    const factory = s.o && effects[s.o.kind];
    if (!factory) return null;
    const r = factory(s);
    return { sync: () => { }, step: () => { }, settle: () => { }, ...r };
}

function animating(s) {
    return !!s.renderer && s.running && s.inView && !document.hidden && !(s.respect && reducedMotionQuery?.matches) && s.w > 0 && s.h > 0;
}

function paint(s) {
    const ctx = s.ctx;
    ctx.setTransform(s.dpr, 0, 0, s.dpr, 0, 0);
    ctx.clearRect(0, 0, s.w, s.h);
    if (!s.renderer || s.w <= 0 || s.h <= 0) return;
    ctx.save();
    s.renderer.draw(ctx);
    ctx.restore();
}

function frame(s, now) {
    s.raf = 0;
    if (!animating(s)) { s.last = 0; return; }
    let dt = s.last ? Math.min((now - s.last) / 1000, 0.25) : 0;
    s.last = now;
    dt *= Math.max(s.o.speed ?? 1, 0);
    // A long frame is split up, so fast movers do not tunnel or jump.
    while (dt > 0) {
        const step = Math.min(dt, 1 / 30);
        s.time += step;
        s.renderer.step(step);
        dt -= step;
    }
    paint(s);
    s.raf = requestAnimationFrame(t => frame(s, t));
}

function refresh(s) {
    if (animating(s)) {
        if (!s.raf) { s.last = 0; s.raf = requestAnimationFrame(t => frame(s, t)); }
    } else {
        if (s.raf) { cancelAnimationFrame(s.raf); s.raf = 0; }
        s.last = 0;
        s.renderer?.settle();
        paint(s);
    }
}

function resize(s) {
    const rect = s.canvas.getBoundingClientRect();
    const w = rect.width, h = rect.height, dpr = window.devicePixelRatio || 1;
    if (w === s.w && h === s.h && dpr === s.dpr) return;
    const first = s.w <= 0 || s.h <= 0;
    s.w = w; s.h = h; s.dpr = dpr;
    s.canvas.width = Math.max(1, Math.round(w * dpr));
    s.canvas.height = Math.max(1, Math.round(h * dpr));
    if (w > 0 && h > 0) s.renderer?.sync(first);
    refresh(s);
}

function parse(json) {
    try { return json ? JSON.parse(json) : null; } catch { return null; }
}

export function init(id, root, canvas, json, running, respect, fixed) {
    if (instances.has(id) || !root || !canvas) return;

    const s = {
        root, canvas, ctx: canvas.getContext('2d'), o: parse(json), renderer: null,
        w: 0, h: 0, dpr: 1, time: 0, last: 0, raf: 0, pointer: null,
        running, respect, fixed, inView: true, listeners: []
    };
    s.renderer = createRenderer(s);
    instances.set(id, s);

    const on = (target, type, fn, opts) => { target.addEventListener(type, fn, opts); s.listeners.push(() => target.removeEventListener(type, fn, opts)); };

    // Pointer position relative to the canvas, which is what the effects draw in. Listening on the
    // window means content on top still reports it, and a Fixed background follows the pointer over
    // the whole page; passive, so scrolling and clicks are untouched. A lifted finger has no hover,
    // so touch clears it again.
    const still = () => { if (!s.raf) { s.renderer?.settle(); paint(s); } };
    const track = e => {
        const r = canvas.getBoundingClientRect();
        const x = e.clientX - r.left, y = e.clientY - r.top;
        s.pointer = x >= 0 && y >= 0 && x <= r.width && y <= r.height ? { x, y } : null;
        still();
    };
    const clear = () => { if (s.pointer) { s.pointer = null; still(); } };
    on(window, 'pointermove', track, { passive: true });
    on(window, 'pointerdown', track, { passive: true });
    on(window, 'pointerup', e => { if (e.pointerType !== 'mouse') clear(); }, { passive: true });
    on(window, 'pointercancel', clear, { passive: true });
    on(document.documentElement, 'pointerleave', clear, { passive: true });

    on(document, 'visibilitychange', () => refresh(s));
    if (reducedMotionQuery) on(reducedMotionQuery, 'change', () => refresh(s));
    on(window, 'resize', () => resize(s));

    s.resizeObserver = new ResizeObserver(() => resize(s));
    s.resizeObserver.observe(canvas);

    // Off screen it costs nothing: a long page of these runs only the ones you can see.
    s.intersectionObserver = new IntersectionObserver(entries => {
        s.inView = entries[entries.length - 1].isIntersecting;
        refresh(s);
    });
    s.intersectionObserver.observe(fixed ? root : canvas);

    resize(s);
}

export function update(id, json, running, respect, fixed) {
    const s = instances.get(id);
    if (!s) return;

    const o = parse(json);
    const kindChanged = (o?.kind) !== (s.o?.kind);
    s.o = o;
    s.running = running;
    s.respect = respect;
    if (fixed !== s.fixed) {
        s.fixed = fixed;
        s.intersectionObserver.disconnect();
        s.intersectionObserver.observe(fixed ? s.root : s.canvas);
    }

    if (kindChanged) {
        s.renderer = createRenderer(s);
        if (s.w > 0 && s.h > 0) s.renderer?.sync(true);
    } else if (s.w > 0 && s.h > 0) {
        s.renderer?.sync(false);
    }
    refresh(s);
}

export function dispose(id) {
    const s = instances.get(id);
    if (!s) return;
    instances.delete(id);
    if (s.raf) cancelAnimationFrame(s.raf);
    s.resizeObserver?.disconnect();
    s.intersectionObserver?.disconnect();
    for (const off of s.listeners) off();
}
