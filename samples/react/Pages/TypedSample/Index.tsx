import type { InertiaPageProps } from '../../types/inertia.generated';

export default function TypedSampleIndex(props: InertiaPageProps['TypedSample/Index']) {
  return (
    <main>
      <h1>Typed sample</h1>
      <p>Users: {props.users.length}</p>
      <p>Total: {props.total_count}</p>
      <ul>
        {props.labels.map((label) => (
          <li key={label}>{label}</li>
        ))}
      </ul>
    </main>
  );
}
