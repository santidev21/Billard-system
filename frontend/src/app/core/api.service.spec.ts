import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ApiService } from './api.service';

/**
 * Contract tests: pin the URL, HTTP verb and body of every endpoint the frontend
 * calls, so a backend route change cannot silently break the client.
 */
describe('ApiService', () => {
  let service: ApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ApiService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('calls the auth endpoints', async () => {
    const login = service.login('demo', 'secret');
    http.expectOne({ url: '/api/auth/login', method: 'POST' }).flush({});
    await login;

    const refresh = service.refresh('r');
    http.expectOne({ url: '/api/auth/refresh', method: 'POST' }).flush({});
    await refresh;

    const logout = service.logout('r');
    http.expectOne({ url: '/api/auth/logout', method: 'POST' }).flush(null);
    await logout;

    const forgot = service.forgotPassword('demo');
    http.expectOne({ url: '/api/auth/forgot', method: 'POST' }).flush(null);
    await forgot;

    const reset = service.resetPassword('demo', '12345678', 'newpassword');
    http.expectOne({ url: '/api/auth/reset', method: 'POST' }).flush(null);
    await reset;

    const change = service.changePassword('u1', 'old', 'new');
    http.expectOne({ url: '/api/auth/change-password', method: 'POST' }).flush(null);
    await change;

    const force = service.forceChangePassword('new');
    http.expectOne({ url: '/api/auth/force-change-password', method: 'POST' }).flush({});
    await force;

    expect().nothing();
  });

  it('calls the admin table endpoints', async () => {
    const list = service.getTables();
    http.expectOne({ url: '/api/tables', method: 'GET' }).flush([]);
    await list;

    const create = service.createTable('Mesa 2', 9000, 'M2');
    const createReq = http.expectOne({ url: '/api/tables', method: 'POST' });
    expect(createReq.request.body).toEqual({ name: 'Mesa 2', hourlyRate: 9000, code: 'M2' });
    createReq.flush({});
    await create;

    const update = service.updateTable('t1', 'Mesa 2', 9000);
    http.expectOne({ url: '/api/tables/t1', method: 'PUT' }).flush({});
    await update;

    const rates = service.updateAllRates(10000);
    http.expectOne({ url: '/api/tables/rate/all', method: 'PUT' }).flush({ updated: 3 });
    await rates;

    const attend = service.attendTable('t1');
    http.expectOne({ url: '/api/tables/t1/attend', method: 'POST' }).flush({});
    await attend;

    const disable = service.disableTable('t1');
    http.expectOne({ url: '/api/tables/t1/disable', method: 'POST' }).flush({});
    await disable;

    const enable = service.enableTable('t1');
    http.expectOne({ url: '/api/tables/t1/enable', method: 'POST' }).flush({});
    await enable;

    const remove = service.deleteTable('t1');
    http.expectOne({ url: '/api/tables/t1', method: 'DELETE' }).flush({ ok: true });
    await remove;
  });

  it('defaults tenant reads to the demo slug', async () => {
    const tables = service.getTenantTables('');
    http.expectOne({ url: '/api/t/demo/tables', method: 'GET' }).flush([]);
    await tables;

    const table = service.getTenantTable('', 'M1');
    http.expectOne({ url: '/api/t/demo/tables/M1', method: 'GET' }).flush({});
    await table;

    const products = service.getTenantProducts('demo');
    http.expectOne({ url: '/api/t/demo/products', method: 'GET' }).flush([]);
    await products;

    expect().nothing();
  });

  it('calls the player session endpoints with transaction ids', async () => {
    const start = service.startSession('demo', 't1', 'A', 'B', 'Managed', 'tx-start');
    const startReq = http.expectOne({ url: '/api/t/demo/tables/t1/start', method: 'POST' });
    expect(startReq.request.body).toEqual({
      whitePlayerName: 'A',
      yellowPlayerName: 'B',
      gameMode: 'Managed',
      transactionId: 'tx-start',
    });
    startReq.flush({ tableId: 't1', matchId: 'm1' });
    await start;

    const score = service.score('demo', 't1', 'yellow', 4, 'tx-score');
    const scoreReq = http.expectOne({ url: '/api/t/demo/tables/t1/score', method: 'POST' });
    expect(scoreReq.request.body).toEqual({
      playerColor: 'yellow',
      delta: 4,
      transactionId: 'tx-score',
    });
    scoreReq.flush({ newScore: 4 });
    await score;

    const rename = service.renamePlayers('demo', 't1', 'A', 'B', 'tx-rename');
    http.expectOne({ url: '/api/t/demo/tables/t1/players', method: 'POST' }).flush(null);
    await rename;

    const waiter = service.callWaiter('demo', 't1');
    http.expectOne({ url: '/api/t/demo/tables/t1/call-waiter', method: 'POST' }).flush(null);
    await waiter;

    const check = service.requestCheck('demo', 't1');
    http.expectOne({ url: '/api/t/demo/tables/t1/request-check', method: 'POST' }).flush(null);
    await check;

    const consumption = service.addConsumption('demo', 't1', 'p1', 2, 'tx-add');
    http.expectOne({ url: '/api/t/demo/tables/t1/consumption', method: 'POST' }).flush({
      consumptionTotal: 6000,
    });
    await consumption;

    const updateConsumption = service.updateConsumption('demo', 't1', 'c1', 3, 'tx-upd');
    http.expectOne({ url: '/api/t/demo/tables/t1/consumption/c1', method: 'PUT' }).flush({
      consumptionTotal: 9000,
    });
    await updateConsumption;

    const deleteConsumption = service.deleteConsumption('demo', 't1', 'c1');
    http.expectOne({ url: '/api/t/demo/tables/t1/consumption/c1', method: 'DELETE' }).flush({
      consumptionTotal: 0,
    });
    await deleteConsumption;

    const finish = service.finishSession('demo', 't1', 'tx-finish');
    http.expectOne({ url: '/api/t/demo/tables/t1/finish', method: 'POST' }).flush({});
    await finish;

    const round = service.finishRound('demo', 't1', 'tx-round');
    http.expectOne({ url: '/api/t/demo/tables/t1/finish-round', method: 'POST' }).flush({});
    await round;

    const rounds = service.getRounds('demo', 't1');
    http.expectOne({ url: '/api/t/demo/tables/t1/rounds', method: 'GET' }).flush({});
    await rounds;
  });

  it('calls the catalog, settings, history and dashboard endpoints', async () => {
    const products = service.getProducts();
    http.expectOne({ url: '/api/products', method: 'GET' }).flush([]);
    await products;

    const create = service.createProduct('Agua', 3000);
    http.expectOne({ url: '/api/products', method: 'POST' }).flush({});
    await create;

    const update = service.updateProduct('p1', 'Agua', 3500);
    http.expectOne({ url: '/api/products/p1', method: 'PUT' }).flush(null);
    await update;

    const deactivate = service.deactivateProduct('p1');
    http.expectOne({ url: '/api/products/p1', method: 'DELETE' }).flush(null);
    await deactivate;

    const settings = service.getSettings();
    http.expectOne({ url: '/api/settings', method: 'GET' }).flush({});
    await settings;

    const saveSettings = service.updateSettings({ HourlyRate: '12000' });
    http.expectOne({ url: '/api/settings', method: 'PUT' }).flush(null);
    await saveSettings;

    const matches = service.getMatches();
    http.expectOne({ url: '/api/matches', method: 'GET' }).flush([]);
    await matches;

    const match = service.getMatch('m1');
    http.expectOne({ url: '/api/matches/m1', method: 'GET' }).flush({});
    await match;

    const summary = service.getDashboardSummary();
    http.expectOne({ url: '/api/dashboard/summary', method: 'GET' }).flush({});
    await summary;

    const top = service.getTopProducts();
    http.expectOne({ url: '/api/dashboard/top-products', method: 'GET' }).flush([]);
    await top;

    const audit = service.getAuditLogs();
    http.expectOne({ url: '/api/audit/logs', method: 'GET' }).flush([]);
    await audit;

    expect().nothing();
  });

  it('calls the super-admin endpoints', async () => {
    const locals = service.getSuperLocals();
    http.expectOne({ url: '/api/super/locals', method: 'GET' }).flush([]);
    await locals;

    const create = service.createLocal('Nuevo Local', 'admin123');
    const createReq = http.expectOne({ url: '/api/super/locals', method: 'POST' });
    expect(createReq.request.body).toEqual({ name: 'Nuevo Local', initialPassword: 'admin123' });
    createReq.flush({});
    await create;

    const recoveries = service.getSuperRecoveries();
    http.expectOne({ url: '/api/super/recoveries', method: 'GET' }).flush([]);
    await recoveries;

    const reveal = service.revealRecovery('r1');
    http
      .expectOne({ url: '/api/super/recoveries/r1/reveal', method: 'POST' })
      .flush({ code: '12345678' });
    await reveal;
  });
});
