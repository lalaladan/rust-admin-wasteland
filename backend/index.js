const WebSocket = require('ws');
const mysql = require('mysql2/promise');
const express = require('express'); // Подключаем Express
const cors = require('cors'); // 👈 НОВОЕ: Подключаем CORS
const path = require('path');

const RUST_IP = process.env.RUST_IP;
const RUST_RCON_PORT = process.env.RUST_RCON_PORT;
const RUST_RCON_PASSWORD = process.env.RUST_RCON_PASSWORD;

let db;
let ws; // Выносим ws наверх, чтобы веб-сервер мог его использовать
let onlinePlayers = []; // 👈 НОВОЕ: Тут будем хранить текущий онлайн

// --- ВЕБ-СЕРВЕР (API) ---
const app = express();

// 👈 НОВОЕ: Разрешаем нашему будущему сайту делать запросы к API
app.use(cors());

app.get('/', (req, res) => {
    // Собираем точный путь к файлу внутри Docker
    const indexPath = path.join(__dirname, 'frontend', 'index.html');
    console.log('👀 Ищем файл сайта по пути:', indexPath);
    
    // Пытаемся отдать файл браузеру
    res.sendFile(indexPath, (err) => {
        if (err) {
            console.error('❌ Ошибка отдачи файла:', err.message);
            res.status(500).send(`
                <h2>Ошибка!</h2>
                <p>Node.js ищет файл по пути: <b>${indexPath}</b>, но его там нет.</p>
                <p>Проверь структуру папок!</p>
            `);
        }
    });
});

app.use(express.static('frontend'));

app.use(express.static(path.join(__dirname, 'frontend')));

// Создаем специальную ссылку (роут) для отправки сообщений
app.get('/api/say', async (req, res) => {
    // Берем текст из ссылки
    const messageText = req.query.text; 

    if (!messageText) {
        return res.send('❌ Ошибка: ты не указал текст сообщения!');
    }

    if (!ws || ws.readyState !== WebSocket.OPEN) {
        return res.send('❌ Ошибка: нет связи с Rust сервером!');
    }

    // 1. Отправляем команду в Rust
    const command = {
        Identifier: 99,
        Message: `say ${messageText}`,
        Name: 'WebPanel'
    };
    ws.send(JSON.stringify(command));
    console.log(`🚀 Отправлено на сервер: [Админ Panel] ${messageText}`);

    // 2. Сохраняем в MySQL
    try {
        await db.query(
            'INSERT INTO chat_logs (player_name, message) VALUES (?, ?)',
            ['SERVER (Admin Panel)', messageText]
        );
        res.send(`✅ Сообщение "${messageText}" отправлено в игру и сохранено в БД!`);
    } catch (err) {
        console.error('⚠️ Ошибка БД:', err.message);
        res.send('⚠️ Ошибка сохранения в БД');
    }
});

// НОВЫЙ РОУТ: Отдаем список игроков сайту
app.get('/api/players', (req, res) => {
    res.json(onlinePlayers);
});

// 2. НОВЫЙ РОУТ: Выдача истории чата из базы данных
app.get('/api/chat', async (req, res) => {
    try {
        // Берем последние 50 сообщений, сортируем от новых к старым
        const [rows] = await db.query('SELECT * FROM chat_logs ORDER BY created_at DESC LIMIT 50');
        
        // Отдаем данные браузеру в формате JSON
        res.json(rows); 
    } catch (err) {
        console.error('⚠️ Ошибка при получении истории чата:', err.message);
        res.status(500).json({ error: 'Ошибка при чтении из базы данных' });
    }
});

// НОВЫЙ РОУТ: Кик игрока
app.get('/api/kick', (req, res) => {
    const steamId = req.query.steamid;
    const reason = req.query.reason || 'Kicked via Admin Panel';

    if (!steamId) return res.status(400).send('❌ Ошибка: не указан SteamID');
    if (!ws || ws.readyState !== WebSocket.OPEN) return res.status(500).send('❌ Нет связи с сервером');

    // Отправляем серверу команду на кик
    const command = {
        Identifier: 888,
        Message: `kick ${steamId} "${reason}"`,
        Name: 'WebPanel'
    };
    ws.send(JSON.stringify(command));
    
    console.log(`🥾 Отправлен кик: ${steamId} Причина: ${reason}`);
    res.send(`✅ Команда на кик отправлена!`);
});

// НОВЫЙ РОУТ: Кик игрока
app.get('/api/kick', (req, res) => {
    const steamId = req.query.steamid;
    const reason = req.query.reason || 'Kicked via Admin Panel';

    if (!steamId) return res.status(400).send('❌ Ошибка: не указан SteamID');
    if (!ws || ws.readyState !== WebSocket.OPEN) return res.status(500).send('❌ Нет связи с сервером');

    // Отправляем серверу команду на кик
    const command = {
        Identifier: 888,
        Message: `kick ${steamId} "${reason}"`,
        Name: 'WebPanel'
    };
    ws.send(JSON.stringify(command));
    
    console.log(`🥾 Отправлен кик: ${steamId} Причина: ${reason}`);
    res.send(`✅ Команда на кик отправлена!`);
});

app.listen(3000, () => {
    console.log('🌐 API сервер запущен! Порт: 3000');
});
// ------------------------

// Подключение к БД
async function initDB() {
    try {
        db = mysql.createPool({
            host: process.env.DB_HOST,
            user: process.env.DB_USER,
            password: process.env.DB_PASSWORD,
            database: process.env.DB_NAME,
            waitForConnections: true,
            connectionLimit: 10,
            queueLimit: 0
        });

        await db.query(`
            CREATE TABLE IF NOT EXISTS chat_logs (
                id INT AUTO_INCREMENT PRIMARY KEY,
                player_name VARCHAR(255) NOT NULL,
                message TEXT NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        `);
        console.log('✅ База данных подключена, таблица chat_logs готова!');

        await db.query(`
            CREATE TABLE IF NOT EXISTS chat_logs (
                id INT AUTO_INCREMENT PRIMARY KEY,
                player_name VARCHAR(255) NOT NULL,
                message TEXT NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        `);

        // 👇 НОВАЯ ТАБЛИЦА: Для комбат-логов
        await db.query(`
            CREATE TABLE IF NOT EXISTS combat_logs (
                id INT AUTO_INCREMENT PRIMARY KEY,
                attacker VARCHAR(255),
                target VARCHAR(255),
                weapon VARCHAR(100),
                body_part VARCHAR(100),
                distance FLOAT,
                damage FLOAT,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        `);
        console.log('✅ База данных подключена, таблицы chat_logs и combat_logs готовы!');
        connectToRust();

    } catch (err) {
        console.error('⚠️ Ошибка БД:', err.message);
        setTimeout(initDB, 3000);
    }
}

// Подключение к Rust
function connectToRust() {
    ws = new WebSocket(`ws://${RUST_IP}:${RUST_RCON_PORT}/${RUST_RCON_PASSWORD}`);

    ws.on('open', () => {
        console.log('✅ Успешно подключено к WebRCON!');
        
        // Как только подключились, начинаем запрашивать список игроков каждые 5 секунд
        setInterval(() => {
            if (ws.readyState === WebSocket.OPEN) {
                // Отправляем команду playerlist. Identifier: 777 нужен, чтобы мы узнали ответ
                ws.send(JSON.stringify({ Identifier: 777, Message: 'playerlist', Name: 'WebPanel' }));
            }
        }, 5000);
    });

    ws.on('message', async (data) => {
        const response = JSON.parse(data);
        if (!response.Message) return;

        // 1. Игнорируем технический ответ playerlist
        if (response.Identifier === 777) {
            try { onlinePlayers = JSON.parse(response.Message); } catch (e) {}
            return; 
        }

        // 2. Ловим обычный чат
        if (response.Type === 'Chat') {
            try {
                const chatData = JSON.parse(response.Message);
                await db.query(
                    'INSERT INTO chat_logs (player_name, message) VALUES (?, ?)',
                    [chatData.Username, chatData.Message]
                );
            } catch (err) {}
        } 
        
        // 3. 👈 НОВОЕ: Ловим Комбат-Логи и Админские сообщения
        else if (response.Type === 'Generic') {
            const text = response.Message;

            // Если это наш модифицированный комбат-лог
            if (text.includes('[WebPanel-CombatLog]')) {
                try {
                    // Отрезаем всё лишнее до фигурной скобки JSON
                    const jsonString = text.substring(text.indexOf('{'));
                    const logData = JSON.parse(jsonString);

                    // Считаем нанесенный урон
                    const damage = (logData.HealthOld - logData.HealthNew).toFixed(1);

                    console.log(`⚔️ [Комбат] ${logData.AttackerSteamId} попал в ${logData.TargetSteamId} (${logData.Area})`);

                    // Записываем в нашу новую таблицу
                    await db.query(
                        'INSERT INTO combat_logs (attacker, target, weapon, body_part, distance, damage) VALUES (?, ?, ?, ?, ?, ?)',
                        [
                            logData.AttackerSteamId, 
                            logData.TargetSteamId, 
                            logData.Weapon, 
                            logData.Area,     // Часть тела (head, chest и тд)
                            logData.Distance, // Дистанция
                            damage            // Урон
                        ]
                    );
                } catch (err) {
                    console.error('⚠️ Ошибка парсинга комбат-лога:', err.message);
                }
            }
            
            // Если это сообщение из серверной консоли (say)
            else if (text.startsWith('[Server Console]') || text.startsWith('[SERVER]')) {
                const cleanMessage = text.replace(/\[Server Console\]|\[SERVER\]/gi, '').trim();
                try {
                    await db.query('INSERT INTO chat_logs (player_name, message) VALUES (?, ?)', ['SERVER', cleanMessage]);
                } catch (err) {}
            }
        }
    });

    ws.on('close', () => {
        console.log('❌ Соединение с сервером закрыто.');
        onlinePlayers = []; // Очищаем список при потере связи
        setTimeout(connectToRust, 5000);
    });
}

initDB();