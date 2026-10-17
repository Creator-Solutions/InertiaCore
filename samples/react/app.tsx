import { createInertiaApp } from '@inertiajs/react';
import type { ComponentType } from 'react';
import { createRoot } from 'react-dom/client';
import type { InertiaPageProps } from './types/inertia.generated';
import TypedSampleIndex from './Pages/TypedSample/Index';
import UsersIndex from './Pages/Users/Index';

/**
 * A typed page component: its props are exactly the props declared for the given
 * component name in the generated declarations.
 */
export type PageComponent<K extends keyof InertiaPageProps> = ComponentType<InertiaPageProps[K]>;

/**
 * The component map. The mapped type forces every component name in
 * `InertiaPageProps` to have a matching entry, and binds each component to the props
 * declared for that name. Renaming a props member in C# and regenerating surfaces a
 * compile error in the page that reads the old name.
 */
export const pages: {
  [K in keyof InertiaPageProps]: PageComponent<K>;
} = {
  'TypedSample/Index': TypedSampleIndex,
  'Users/Index': UsersIndex,
};

void createInertiaApp({
  // The framework types `name` as `string`; narrow it to the registry keys here.
  resolve: (name) => ({ default: pages[name as keyof InertiaPageProps] }),
  setup({ el, App, props }) {
    createRoot(el as HTMLElement).render(<App {...props} />);
  },
});
