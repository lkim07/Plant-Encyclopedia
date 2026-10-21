import { Routes } from '@angular/router';

export const routes: Routes = [
  // "Home / Identify" is the default destination (DEVELOPMENT_ROADMAP Stage 3).
  { path: '', pathMatch: 'full', redirectTo: 'identify' },
  {
    path: 'identify',
    title: 'Identify · Plant Encyclopedia',
    loadComponent: () => import('./features/identify/identify-page').then((m) => m.IdentifyPage),
  },
  {
    path: 'explore',
    title: 'Explore · Plant Encyclopedia',
    loadComponent: () => import('./features/explore/explore-page').then((m) => m.ExplorePage),
  },
  {
    path: 'favorites',
    title: 'Favorites · Plant Encyclopedia',
    loadComponent: () => import('./features/favorites/favorites-page').then((m) => m.FavoritesPage),
  },
  {
    path: 'search',
    title: 'Search · Plant Encyclopedia',
    loadComponent: () => import('./features/search/search-page').then((m) => m.SearchPage),
  },
  {
    path: 'categories/:categoryId',
    title: 'Category · Plant Encyclopedia',
    loadComponent: () => import('./features/category/category-page').then((m) => m.CategoryPage),
  },
  {
    path: 'plant-groups/:plantGroupId',
    title: 'Plant Group · Plant Encyclopedia',
    loadComponent: () =>
      import('./features/plant-group/plant-group-page').then((m) => m.PlantGroupPage),
  },
  {
    path: 'plants/:plantId',
    title: 'Plant · Plant Encyclopedia',
    loadComponent: () =>
      import('./features/plant-detail/plant-detail-page').then((m) => m.PlantDetailPage),
  },
  {
    path: 'login',
    title: 'Sign in · Plant Encyclopedia',
    loadComponent: () => import('./features/account/login-page').then((m) => m.LoginPage),
  },
  {
    path: 'signup',
    title: 'Create an account · Plant Encyclopedia',
    loadComponent: () => import('./features/account/signup-page').then((m) => m.SignupPage),
  },
  {
    path: 'forgot-password',
    title: 'Forgot password · Plant Encyclopedia',
    loadComponent: () =>
      import('./features/account/password-pages').then((m) => m.ForgotPasswordPage),
  },
  {
    path: 'reset-password',
    title: 'Choose a new password · Plant Encyclopedia',
    loadComponent: () =>
      import('./features/account/password-pages').then((m) => m.ResetPasswordPage),
  },
  {
    path: 'email-confirmed',
    title: 'Email confirmation · Plant Encyclopedia',
    loadComponent: () =>
      import('./features/account/password-pages').then((m) => m.EmailConfirmedPage),
  },
  {
    path: '**',
    title: 'Page not found · Plant Encyclopedia',
    loadComponent: () => import('./features/not-found/not-found-page').then((m) => m.NotFoundPage),
  },
];
