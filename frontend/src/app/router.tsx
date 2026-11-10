import { Navigate, type RouteObject } from 'react-router'
import { GrantTasksPage } from '@/pages/GrantTasksPage'
import { AppLayout } from './AppLayout'

/** The selected grant lives in the URL, so refresh, back/forward and deep links work. */
export const routes: RouteObject[] = [
  {
    element: <AppLayout />,
    children: [
      { index: true, element: <GrantTasksPage /> },
      { path: 'grants/:grantId', element: <GrantTasksPage /> },
      { path: '*', element: <Navigate to="/" replace /> },
    ],
  },
]
