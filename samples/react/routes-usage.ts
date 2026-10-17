import { route, type InertiaRouteName } from './types/routes';

// Valid: a zero-parameter route may be called with no arguments.
export const usersIndex = route('users.index');

// Valid: required parameters are supplied by name.
export const usersShow = route('users.show', { id: 5 });

// Valid: query-string extras are allowed on top of the route parameters.
export const usersShowPaged = route('users.show', { id: 5, page: 2 });

// Valid: catch-all parameters accept an array of segments.
export const search = route('search', { rest: ['posts', 'latest'] });

// Valid: route names are a literal union.
export const knownRoutes: InertiaRouteName[] = ['users.index', 'users.show'];

// @ts-expect-error unknown route names are rejected.
route('nope.nope');

// @ts-expect-error required parameters must be supplied.
route('users.show');

// @ts-expect-error unknown names are not part of the union.
export const bad: InertiaRouteName = 'nope.nope';
