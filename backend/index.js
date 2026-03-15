const WebSocket = require('ws');
const mysql = require('mysql2/promise');
const express = require('express');
const cors = require('cors');
const path = require('path');
const fs = require('fs');

const RUST_IP = process.env.RUST_IP;
const RUST_RCON_PORT = process.env.RUST_RCON_PORT;
const RUST_RCON_PASSWORD = process.env.RUST_RCON_PASSWORD;

let db;
let ws; 
let onlinePlayers = []; 
let latestMapData = []; 
let currentSeed = null; 
let currentSize = 4000;
let pendingSeed = null;
let pendingSize = null;

async function downloadMapImage(size, seed) {
    const mapPath = path.join(__dirname, 'frontend', 'map.png');
    const API_KEY = '01df7e0da8f545439003ec6e4af26f2f'; 
    
    try {
        console.log(`🗺️ Запрашиваем готовую карту (Size: ${size}, Seed: ${seed}) у RustMaps...`);
        const res = await fetch(`https://api.rustmaps.com/v4/maps/${size}/${seed}`, {
            headers: { 'X-API-Key': API_KEY }
        });
        if (!res.ok) return;
        
        const json = await res.json();
        const imageUrl = json.data?.imageIconUrl || json.data?.imageUrl; 
        if (!imageUrl) return;

        console.log(`⬇️ Скачиваем изображение карты...`);
        const imageRes = await fetch(imageUrl);
        const buffer = await imageRes.arrayBuffer();
        
        fs.writeFileSync(mapPath, Buffer.from(buffer));
        console.log('✅ Изображение карты успешно скачано!');
    } catch (err) {}
}

const app = express();
app.use(cors());
app.use(express.static(path.join(__dirname, 'frontend')));

app.get('/', (req, res) => {
    res.sendFile(path.join(__dirname, 'frontend', 'index.html'));
});

app.get('/api/say', async (req, res) => {
    const messageText = req.query.text; 
    if (!messageText || !ws || ws.readyState !== WebSocket.OPEN) return res.send('❌ Ошибка');
    ws.send(JSON.stringify({ Identifier: 99, Message: `say ${messageText}`, Name: 'WebPanel' }));
    try {
        await db.query('INSERT INTO chat_logs (player_name, message) VALUES (?, ?)', ['SERVER (Admin Panel)', messageText]);
        res.send(`✅ Отправлено!`);
    } catch (err) { res.send('⚠️ Ошибка БД'); }
});

app.get('/api/players', (req, res) => res.json(onlinePlayers));
app.get('/api/chat', async (req, res) => {
    try {
        const [rows] = await db.query('SELECT * FROM chat_logs ORDER BY created_at DESC LIMIT 50');
        res.json(rows); 
    } catch (err) { res.status(500).json({ error: 'Ошибка БД' }); }
});

app.get('/api/kick', (req, res) => {
    const steamId = req.query.steamid;
    const reason = req.query.reason || 'Kicked via Admin Panel';
    if (!steamId) return res.status(400).send('❌ Ошибка');
    ws.send(JSON.stringify({ Identifier: 888, Message: `kick ${steamId} "${reason}"`, Name: 'WebPanel' }));
    res.send(`✅ Отправлено!`);
});

app.get('/api/combatlog', async (req, res) => {
    const steamId = req.query.steamid; 
    try {
        let query = 'SELECT * FROM combat_logs ORDER BY created_at DESC LIMIT 100';
        let params = [];
        if (steamId) {
            query = 'SELECT * FROM combat_logs WHERE attacker = ? OR target = ? ORDER BY created_at DESC LIMIT 100';
            params = [steamId, steamId];
        }
        const [rows] = await db.query(query, params);
        res.json(rows);
    } catch (err) { res.status(500).json({ error: 'Ошибка БД' }); }
});

app.get('/api/disconnects', async (req, res) => {
    try {
        const [rows] = await db.query('SELECT * FROM disconnect_logs ORDER BY created_at DESC LIMIT 50');
        res.json(rows);
    } catch (err) { res.status(500).json({ error: 'Ошибка БД' }); }
});

app.get('/api/alliance', async (req, res) => {
    const search = req.query.search; 
    try {
        let query = 'SELECT * FROM alliance_logs ORDER BY created_at DESC LIMIT 100';
        let params = [];
        if (search) {
            query = 'SELECT * FROM alliance_logs WHERE steam_id = ? OR location = ? ORDER BY created_at DESC LIMIT 100';
            params = [search, search];
        }
        const [rows] = await db.query(query, params);
        res.json(rows);
    } catch (err) { res.status(500).json({ error: 'Ошибка БД' }); }
});

app.get('/api/map', (req, res) => res.json({ size: currentSize, players: latestMapData }));

app.listen(3000, () => console.log('🌐 API запущен на 3000 порту!'));

async function initDB() {
    try {
        db = mysql.createPool({ host: process.env.DB_HOST, user: process.env.DB_USER, password: process.env.DB_PASSWORD, database: process.env.DB_NAME });
        await db.query(`CREATE TABLE IF NOT EXISTS chat_logs (id INT AUTO_INCREMENT PRIMARY KEY, player_name VARCHAR(255) NOT NULL, message TEXT NOT NULL, created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP)`);
        await db.query(`CREATE TABLE IF NOT EXISTS disconnect_logs (id INT AUTO_INCREMENT PRIMARY KEY, steam_id VARCHAR(255) NOT NULL, player_name VARCHAR(255) NOT NULL, reason TEXT NOT NULL, created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP)`);
        await db.query(`CREATE TABLE IF NOT EXISTS combat_logs (id INT AUTO_INCREMENT PRIMARY KEY, attacker VARCHAR(255), attacker_name VARCHAR(255), target VARCHAR(255), target_name VARCHAR(255), weapon VARCHAR(100), body_part VARCHAR(100), distance FLOAT, damage FLOAT, created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP)`);
        await db.query(`CREATE TABLE IF NOT EXISTS alliance_logs (id INT AUTO_INCREMENT PRIMARY KEY, steam_id VARCHAR(255) NOT NULL, player_name VARCHAR(255) NOT NULL, action VARCHAR(255) NOT NULL, location VARCHAR(255) NOT NULL, created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP)`);
        console.log('✅ БД готова!');
        connectToRust();
    } catch (err) { setTimeout(initDB, 3000); }
}

function connectToRust() {
    ws = new WebSocket(`ws://${RUST_IP}:${RUST_RCON_PORT}/${RUST_RCON_PASSWORD}`);

    ws.on('open', () => {
        console.log('✅ Успешно подключено к WebRCON!');
        
        setInterval(() => {
            if (ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({ Identifier: 777, Message: 'playerlist', Name: 'WebPanel' }));
                ws.send(JSON.stringify({ Identifier: 778, Message: 'webpanel.map', Name: 'WebPanel' }));
                ws.send(JSON.stringify({ Identifier: 782, Message: 'webpanel.combatlog', Name: 'WebPanel' }));
                
                // 👇 НОВОЕ: Запрашиваем логи альянса
                ws.send(JSON.stringify({ Identifier: 783, Message: 'webpanel.alliance', Name: 'WebPanel' }));
                
                if (!currentSeed) {
                    ws.send(JSON.stringify({ Identifier: 780, Message: 'server.seed', Name: 'WebPanel' }));
                    ws.send(JSON.stringify({ Identifier: 781, Message: 'server.worldsize', Name: 'WebPanel' }));
                }
            }
        }, 3000);
    });

    ws.on('message', async (data) => {
        const response = JSON.parse(data);
        if (!response.Message) return;

        if (response.Identifier === 780) {
            const match = response.Message.match(/(\d+)/);
            if (match) pendingSeed = match[1];
            return;
        }

        if (response.Identifier === 781) {
            const match = response.Message.match(/(\d+)/);
            if (match) {
                pendingSize = match[1];
                currentSize = match[1]; 
            }
            return;
        }

        if (pendingSeed && pendingSize && currentSeed !== pendingSeed) {
            currentSeed = pendingSeed; 
            downloadMapImage(pendingSize, pendingSeed);
        }

        if (response.Identifier === 777) {
            try { onlinePlayers = JSON.parse(response.Message); } catch (e) {}
            return; 
        }
        if (response.Identifier === 778) {
            try { latestMapData = JSON.parse(response.Message); } catch (e) {}
            return; 
        }

        if (response.Identifier === 782) {
            try { 
                const logsArray = JSON.parse(response.Message); 
                for (const logData of logsArray) {
                    if (logData.TargetSteamId === '0' || logData.TargetSteamId === 0) continue;

                    const isPlayer = logData.TargetSteamId.toString().length === 17;
                    if (!isPlayer) {
                        const tName = logData.TargetName.toLowerCase();
                        const junkWords = ['barrel', 'crate', 'box', 'stash', 'tree', 'node', 'ore', 'door', 'wall', 'building', 'window', 'hemp', 'plant', 'corpse', 'sleepingbag', 'bed', 'cupboard', 'furnace', 'campfire', 'sign'];
                        if (junkWords.some(word => tName.includes(word))) continue;
                    }

                    const damage = Math.abs(logData.HealthOld - logData.HealthNew).toFixed(1);
                    await db.query('INSERT INTO combat_logs (attacker, attacker_name, target, target_name, weapon, body_part, distance, damage) VALUES (?, ?, ?, ?, ?, ?, ?, ?)',
                        [logData.AttackerSteamId, logData.AttackerName, logData.TargetSteamId, logData.TargetName, logData.Weapon, logData.Area, logData.Distance, damage]);
                }
            } catch (e) {}
            return; 
        }

        // 👇 НОВОЕ: Тихо ловим массив альянса и кладем в БД
        if (response.Identifier === 783) {
            try { 
                const logsArray = JSON.parse(response.Message); 
                for (const logData of logsArray) {
                    await db.query('INSERT INTO alliance_logs (steam_id, player_name, action, location) VALUES (?, ?, ?, ?)',
                        [logData.SteamID, logData.Name, logData.Action, logData.Location]);
                }
            } catch (e) {}
            return; 
        }

        if (response.Type === 'Chat') {
            try {
                const chatData = JSON.parse(response.Message);
                await db.query('INSERT INTO chat_logs (player_name, message) VALUES (?, ?)', [chatData.Username, chatData.Message]);
            } catch (err) {}
        } 
        else if (response.Type === 'Generic') {
            const text = response.Message;
            
            const connectionMatch = text.match(/\/(\d{17})\/(.+?) (joined|disconnecting:)\s*(.*)/);
            if (connectionMatch) {
                const steamId = connectionMatch[1];
                const playerName = connectionMatch[2];
                const isJoin = connectionMatch[3] === 'joined';
                let reasonText = isJoin ? 'Вход на сервер' : `Выход (${connectionMatch[4] || 'без причины'})`;
                try { await db.query('INSERT INTO disconnect_logs (steam_id, player_name, reason) VALUES (?, ?, ?)', [steamId, playerName, reasonText]); } catch (err) {}
            }

            if (text.startsWith('[Server Console]') || text.startsWith('[SERVER]')) {
                const cleanMessage = text.replace(/\[Server Console\]|\[SERVER\]/gi, '').trim();
                try { await db.query('INSERT INTO chat_logs (player_name, message) VALUES (?, ?)', ['SERVER', cleanMessage]); } catch (err) {}
            }
        }
    });

    ws.on('close', () => {
        onlinePlayers = []; 
        setTimeout(connectToRust, 5000);
    });
}

initDB();