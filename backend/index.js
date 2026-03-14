const WebSocket = require('ws');
const mysql = require('mysql2/promise');

// Берем настройки прямо из переменных окружения docker-compose.yml
const RUST_IP = process.env.RUST_IP;
const RUST_RCON_PORT = process.env.RUST_RCON_PORT;
const RUST_RCON_PASSWORD = process.env.RUST_RCON_PASSWORD;

let db;

// 1. Подключаемся к БД и создаем таблицу
// 1. Подключаемся к БД и создаем таблицу
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

        // Пытаемся создать таблицу. Если БД еще спит, здесь будет ошибка
        await db.query(`
            CREATE TABLE IF NOT EXISTS chat_logs (
                id INT AUTO_INCREMENT PRIMARY KEY,
                player_name VARCHAR(255) NOT NULL,
                message TEXT NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        `);
        console.log('✅ База данных подключена, таблица chat_logs готова!');
        
        // Как только БД готова, подключаемся к серверу Rust
        connectToRust();
    } catch (err) {
        console.error('⚠️ Ошибка подключения к БД:', err.message);
        console.log('⏳ База данных еще загружается. Пробуем снова через 3 секунды...');
        setTimeout(initDB, 3000); // Пробуем заново через 3 секунды
    }
}

// 2. Подключаемся к WebRCON
function connectToRust() {
    console.log('⏳ Подключаемся к Rust серверу...');
    const ws = new WebSocket(`ws://${RUST_IP}:${RUST_RCON_PORT}/${RUST_RCON_PASSWORD}`);

    ws.on('open', () => {
        console.log('✅ Успешно подключено к WebRCON!');
    });

    ws.on('message', async (data) => {
        const response = JSON.parse(data);
        if (!response.Message) return;

        if (response.Type === 'Chat') {
            // ... (тут остается твой рабочий код для обычного чата)
            try {
                const chatData = JSON.parse(response.Message);
                const playerName = chatData.Username;
                const messageText = chatData.Message;
                console.log(`💬 [Чат] ${playerName}: ${messageText}`);
                await db.query(
                    'INSERT INTO chat_logs (player_name, message) VALUES (?, ?)',
                    [playerName, messageText]
                );
            } catch (err) {
                console.error('⚠️ Ошибка при сохранении сообщения:', err.message);
            }
        } else {
            // 🕵️ ВРЕМЕННЫЙ ДЕБАГ: Выводим абсолютно всё остальное, что шлет сервер
            // Как только поймаем формат /say, мы это удалим, чтобы не спамить консоль
            console.log(`[RAW RCON ШПИОН] Type: ${response.Type} | Message: ${response.Message}`);
        }
    });

    ws.on('close', () => {
        console.log('❌ Соединение с сервером закрыто. Пробуем переподключиться через 5 сек...');
        setTimeout(connectToRust, 5000);
    });

    ws.on('error', (err) => {
        console.error('⚠️ Ошибка RCON:', err.message);
    });
}

// Запускаем весь 
initDB();