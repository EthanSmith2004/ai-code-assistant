import express from 'express';
import { cartController } from './controllers/cartController';

const app = express();
app.use(express.json());

app.get('/cart/:id', cartController.get);
app.post('/cart/:id/items', cartController.addItem);
app.delete('/cart/:id', cartController.clear);

app.listen(3000);
