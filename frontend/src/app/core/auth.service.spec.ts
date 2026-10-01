import { TestBed } from '@angular/core/testing';

import { ApiService } from './api.service';
import { AuthService } from './auth.service';
import { LoginResponse } from './models';

describe('AuthService', () => {
  let service: AuthService;
  let api: jasmine.SpyObj<ApiService>;

  const response: LoginResponse = {
    accessToken: 'access-token',
    refreshToken: 'refresh-token',
    userName: 'Admin',
    role: 'Administrator',
    tenantName: 'Demo Hall',
    tenantSlug: 'demo',
    mustChangePassword: false,
  };

  beforeEach(() => {
    localStorage.clear();
    api = jasmine.createSpyObj<ApiService>('ApiService', [
      'login',
      'refresh',
      'forceChangePassword',
      'logout',
    ]);
    TestBed.configureTestingModule({
      providers: [AuthService, { provide: ApiService, useValue: api }],
    });
    service = TestBed.inject(AuthService);
  });

  afterEach(() => localStorage.clear());

  it('starts unauthenticated', () => {
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.getToken()).toBeNull();
    expect(service.getUser()).toBeNull();
    expect(service.getTenantSlug()).toBeNull();
  });

  it('stores tokens and user info on login', async () => {
    api.login.and.resolveTo(response);

    const result = await service.login('demo', 'secret');

    expect(result).toBe(response);
    expect(service.isAuthenticated()).toBeTrue();
    expect(service.getToken()).toBe('access-token');
    expect(service.getTenantSlug()).toBe('demo');
    expect(service.isSuperAdmin()).toBeFalse();
    expect(service.mustChangePassword()).toBeFalse();
  });

  it('returns null when the stored user payload is not valid JSON', () => {
    localStorage.setItem('billiard-user', '{not-json');

    expect(service.getUser()).toBeNull();
  });

  it('detects super admins and pending password changes', () => {
    localStorage.setItem(
      'billiard-user',
      JSON.stringify({
        name: 'Root',
        role: 'SuperAdmin',
        tenantSlug: null,
        mustChangePassword: true,
      }),
    );

    expect(service.isSuperAdmin()).toBeTrue();
    expect(service.mustChangePassword()).toBeTrue();
  });

  it('throws when refreshing without a refresh token', async () => {
    await expectAsync(service.refresh()).toBeRejectedWithError('No refresh token');
  });

  it('rotates tokens on refresh', async () => {
    localStorage.setItem('billiard-refresh-token', 'old-refresh');
    api.refresh.and.resolveTo({
      ...response,
      accessToken: 'new-access',
      refreshToken: 'new-refresh',
    });

    await service.refresh();

    expect(api.refresh).toHaveBeenCalledWith('old-refresh');
    expect(service.getToken()).toBe('new-access');
  });

  it('clears the session on logout even if the API call fails', async () => {
    api.login.and.resolveTo(response);
    api.logout.and.rejectWith(new Error('offline'));
    await service.login('demo', 'secret');

    await service.logout();

    expect(api.logout).toHaveBeenCalledWith('refresh-token');
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.getUser()).toBeNull();
  });

  it('does not call the API on logout without a session', async () => {
    await service.logout();

    expect(api.logout).not.toHaveBeenCalled();
  });
});
