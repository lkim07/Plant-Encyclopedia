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
    path: 'plants/:plantId',
    title: 'Plant · Plant Encyclopedia',
    loadComponent: () =>
      import('./features/plant-detail/plant-detail-page').then((m) => m.PlantDetailPage),
  },
  {
    path: '**',
    title: 'Page not found · Plant Encyclopedia',
    loadComponent: () => import('./features/not-found/not-found-page').then((m) => m.NotFoundPage),
  },
];
