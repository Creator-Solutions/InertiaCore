import type { InertiaPageProps } from '../../types/inertia.generated';

export default function UsersIndex(props: InertiaPageProps['Users/Index']) {
  return (
    <ul>
      {props.users.map((user) => (
        <li key={user}>{user}</li>
      ))}
    </ul>
  );
}
