import http from 'k6/http';
import { check } from 'k6';

// Yük profili: 10 sanal kullanıcı 30 saniye boyunca POST /api/transfers'e yüklenir.
// Her kullanıcının kendi fonlanmış hesap çifti vardır; böylece ölçülen şey tek bir
// hesap üzerindeki yapay çekişme değil, pipeline'ın kendisi olur. Tutar 1 TRY,
// 20.000'lik günlük transfer limitinin çok altında kalmak için.
//
// Çalıştırma (API http://localhost:5000 dinlerken):
//   docker run --rm -i grafana/k6 run --add-host=host.docker.internal:host-gateway - < loadtest/transfer-load.js
// veya yerel k6 kurulumuyla:
//   k6 run -e BASE_URL=http://localhost:5000 loadtest/transfer-load.js

const BASE_URL = __ENV.BASE_URL || 'http://host.docker.internal:5000';
const PAIRS = 10;
const DEPOSIT = 20000;

export const options = {
    vus: PAIRS,
    duration: '30s',
    thresholds: {
        http_req_failed: ['rate<0.01'],
        http_req_duration: ['p(95)<300'],
        checks: ['rate>0.99'],
    },
};

const jsonHeaders = (token) => ({
    'Content-Type': 'application/json',
    Authorization: `Bearer ${token}`,
});

export function setup() {
    const email = `load-${Date.now()}@bank.local`;
    const password = 'Load-Pass-123!';

    let res = http.post(`${BASE_URL}/api/auth/register`, JSON.stringify({ email, password }),
        { headers: { 'Content-Type': 'application/json' } });
    check(res, { 'register 201': (r) => r.status === 201 });

    res = http.post(`${BASE_URL}/api/auth/login`, JSON.stringify({ email, password }),
        { headers: { 'Content-Type': 'application/json' } });
    check(res, { 'login 200': (r) => r.status === 200 });
    const token = res.json('accessToken');

    const pairs = [];
    for (let i = 0; i < PAIRS; i++) {
        const source = createAccount(token);
        const destination = createAccount(token);

        res = http.post(`${BASE_URL}/api/accounts/${source}/kyc`, null, { headers: jsonHeaders(token) });
        check(res, { 'kyc 204': (r) => r.status === 204 });

        res = http.post(
            `${BASE_URL}/api/accounts/${source}/deposits`,
            JSON.stringify({ amount: DEPOSIT, currencyCode: 'TRY' }),
            { headers: { ...jsonHeaders(token), 'Idempotency-Key': `load-dep-${i}-${Date.now()}` } });
        check(res, { 'deposit 200': (r) => r.status === 200 });

        pairs.push({ source, destination });
    }

    return { token, pairs };
}

function createAccount(token) {
    const res = http.post(`${BASE_URL}/api/accounts`, JSON.stringify({ currencyCode: 'TRY' }),
        { headers: jsonHeaders(token) });
    check(res, { 'create account 201': (r) => r.status === 201 });
    return res.json('id');
}

export default function (data) {
    const pair = data.pairs[(__VU - 1) % PAIRS];

    const res = http.post(
        `${BASE_URL}/api/transfers`,
        JSON.stringify({
            sourceAccountId: pair.source,
            destinationAccountId: pair.destination,
            amount: 1,
            currencyCode: 'TRY',
        }),
        {
            headers: {
                ...jsonHeaders(data.token),
                'Idempotency-Key': `load-${__VU}-${__ITER}-${Date.now()}`,
            },
        });

    check(res, { 'transfer 200': (r) => r.status === 200 });
}

export function teardown(data) {
    // Para korunumu: her çift hâlâ yatırılan toplamın tamamını tutuyor
    for (const pair of data.pairs) {
        const source = http.get(`${BASE_URL}/api/accounts/${pair.source}`, { headers: jsonHeaders(data.token) });
        const destination = http.get(`${BASE_URL}/api/accounts/${pair.destination}`, { headers: jsonHeaders(data.token) });
        const total = source.json('balance') + destination.json('balance');
        if (total !== DEPOSIT) {
            throw new Error(`money not conserved: pair holds ${total}, expected ${DEPOSIT}`);
        }
    }
    console.log('money conservation check passed for all pairs');
}
