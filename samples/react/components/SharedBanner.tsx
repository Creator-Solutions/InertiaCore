import { usePageProps } from '../types/inertia.generated';

/**
 * Demonstrates the generated `usePageProps` helper. The returned props are the page
 * props for the component name intersected with `InertiaSharedProps`, so both the
 * page's own members (`users`) and shared members (`appName`) are typed.
 */
export function SharedBanner() {
  const props = usePageProps('TypedSample/Index');
  return (
    <div>
      <span>{props.appName}</span>
      <span>Users: {props.users.length}</span>
    </div>
  );
}
